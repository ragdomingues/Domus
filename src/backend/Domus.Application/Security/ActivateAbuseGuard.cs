using System.Collections.Concurrent;
using Domus.Application.Abstractions;
using Domus.Application.Common;

namespace Domus.Application.Security;

public interface IActivateAbuseGuard
{
    Result EnsureAllowed(string? ipAddress, string provisioningCode, string hardwareId);
    void RegisterFailure(string? ipAddress, string provisioningCode, string hardwareId);
    void RegisterSuccess(string? ipAddress, string provisioningCode, string hardwareId);
}

/// <summary>
/// In-memory brute-force protection for device activation (IP + code + hardwareId).
/// </summary>
public sealed class ActivateAbuseGuard : IActivateAbuseGuard
{
    private readonly ISecretHasher _hasher;
    private readonly IDateTimeProvider _clock;
    private readonly ConcurrentDictionary<string, Counter> _counters = new();

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private const int MaxIpFailures = 30;
    private const int MaxCodeFailures = 8;
    private const int MaxHardwareFailures = 8;

    public ActivateAbuseGuard(ISecretHasher hasher, IDateTimeProvider clock)
    {
        _hasher = hasher;
        _clock = clock;
    }

    public Result EnsureAllowed(string? ipAddress, string provisioningCode, string hardwareId)
    {
        Prune();

        if (IsBlocked(Key("ip", ipAddress ?? "unknown"), MaxIpFailures))
        {
            return Result.Failure("Muitas tentativas. Tente novamente mais tarde.", "activate_rate_limited");
        }

        if (IsBlocked(Key("code", _hasher.Hash(provisioningCode)), MaxCodeFailures))
        {
            return Result.Failure("Muitas tentativas para este código.", "activate_rate_limited");
        }

        if (IsBlocked(Key("hw", hardwareId.Trim().ToLowerInvariant()), MaxHardwareFailures))
        {
            return Result.Failure("Muitas tentativas para este hardware.", "activate_rate_limited");
        }

        return Result.Success();
    }

    public void RegisterFailure(string? ipAddress, string provisioningCode, string hardwareId)
    {
        Increment(Key("ip", ipAddress ?? "unknown"));
        Increment(Key("code", _hasher.Hash(provisioningCode)));
        Increment(Key("hw", hardwareId.Trim().ToLowerInvariant()));
    }

    public void RegisterSuccess(string? ipAddress, string provisioningCode, string hardwareId)
    {
        _counters.TryRemove(Key("code", _hasher.Hash(provisioningCode)), out _);
        _counters.TryRemove(Key("hw", hardwareId.Trim().ToLowerInvariant()), out _);
    }

    private bool IsBlocked(string key, int max)
    {
        if (!_counters.TryGetValue(key, out var counter))
        {
            return false;
        }

        return counter.Failures >= max && _clock.UtcNow - counter.WindowStart < Window;
    }

    private void Increment(string key)
    {
        _counters.AddOrUpdate(
            key,
            _ => new Counter(1, _clock.UtcNow),
            (_, existing) =>
            {
                if (_clock.UtcNow - existing.WindowStart >= Window)
                {
                    return new Counter(1, _clock.UtcNow);
                }

                return existing with { Failures = existing.Failures + 1 };
            });
    }

    private void Prune()
    {
        foreach (var pair in _counters)
        {
            if (_clock.UtcNow - pair.Value.WindowStart >= Window)
            {
                _counters.TryRemove(pair.Key, out _);
            }
        }
    }

    private static string Key(string kind, string value) => $"{kind}:{value}";

    private sealed record Counter(int Failures, DateTimeOffset WindowStart);
}

using Domus.Application.Abstractions;
using Domus.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Domus.Application.Devices;

public sealed record MqttAuthRequest(string Username, string Password);
public sealed record MqttAclRequest(string Username, string Topic, string Action);

public interface IMqttAuthService
{
    Task<bool> AuthenticateAsync(MqttAuthRequest request, CancellationToken cancellationToken = default);
    Task<bool> AuthorizeAsync(MqttAclRequest request, CancellationToken cancellationToken = default);
}

public sealed class MqttAuthService : IMqttAuthService
{
    private readonly IDomusDbContext _db;
    private readonly ISecretHasher _secretHasher;
    private readonly MqttServiceCredentials _serviceCredentials;

    public MqttAuthService(
        IDomusDbContext db,
        ISecretHasher secretHasher,
        MqttServiceCredentials serviceCredentials)
    {
        _db = db;
        _secretHasher = secretHasher;
        _serviceCredentials = serviceCredentials;
    }

    public async Task<bool> AuthenticateAsync(MqttAuthRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return false;
        }

        if (IsServiceAccount(request.Username))
        {
            return string.Equals(request.Password, _serviceCredentials.Password, StringComparison.Ordinal);
        }

        var device = await _db.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.MqttUsername == request.Username, cancellationToken);

        if (device is null
            || device.LifecycleStatus != DeviceLifecycleStatus.Active
            || string.IsNullOrWhiteSpace(device.MqttSecretHash))
        {
            return false;
        }

        return _secretHasher.Verify(request.Password, device.MqttSecretHash);
    }

    public async Task<bool> AuthorizeAsync(MqttAclRequest request, CancellationToken cancellationToken = default)
    {
        var action = request.Action.ToLowerInvariant();
        var isPublish = action is "publish" or "pub";
        var isSubscribe = action is "subscribe" or "sub";

        if (IsServiceAccount(request.Username))
        {
            if (!MqttTopics.TryParse(request.Topic, out _, out _, out var leaf))
            {
                // wildcards for API subscriber
                return isSubscribe &&
                       (request.Topic is MqttTopics.StatusWildcard or MqttTopics.HeartbeatWildcard
                        || request.Topic.StartsWith("domus/", StringComparison.OrdinalIgnoreCase));
            }

            return leaf switch
            {
                "command" or "config" => isPublish,
                "status" or "heartbeat" => isSubscribe,
                _ => false
            };
        }

        var device = await _db.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.MqttUsername == request.Username, cancellationToken);

        if (device is null || device.LifecycleStatus != DeviceLifecycleStatus.Active)
        {
            return false;
        }

        if (!MqttTopics.TryParse(request.Topic, out var tenantId, out var deviceId, out var topicLeaf))
        {
            return false;
        }

        if (tenantId != device.TenantId || deviceId != device.Id)
        {
            return false;
        }

        return topicLeaf switch
        {
            "command" or "config" => isSubscribe,
            "status" or "heartbeat" => isPublish,
            _ => false
        };
    }

    private bool IsServiceAccount(string username) =>
        string.Equals(username, _serviceCredentials.Username, StringComparison.Ordinal);
}

public sealed class MqttServiceCredentials
{
    public string Username { get; init; } = "domus_api";
    public string Password { get; init; } = "change-me";
}

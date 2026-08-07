using Domus.Application.Abstractions;
using Domus.Application.Common;
using Domus.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Domus.Application.Notifications;

public interface IPushTokenService
{
    Task<Result<PushTokenResponse>> RegisterAsync(
        Guid userId,
        RegisterPushTokenRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> UnregisterAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken = default);

    Task<Result> UnregisterAllAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class PushTokenService : IPushTokenService
{
    private readonly IDomusDbContext _db;

    public PushTokenService(IDomusDbContext db)
    {
        _db = db;
    }

    public async Task<Result<PushTokenResponse>> RegisterAsync(
        Guid userId,
        RegisterPushTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var token = request.Token?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token) || token.Length > 512)
        {
            return Result<PushTokenResponse>.Failure("Token de push inválido.", "invalid_token");
        }

        if (!token.StartsWith("ExponentPushToken[", StringComparison.Ordinal) &&
            !token.StartsWith("ExpoPushToken[", StringComparison.Ordinal))
        {
            return Result<PushTokenResponse>.Failure("Token de push inválido.", "invalid_token");
        }

        // Token pode migrar de usuário (relogin no mesmo aparelho).
        var existing = await _db.UserPushTokens
            .FirstOrDefaultAsync(t => t.Token == token, cancellationToken);

        if (existing is not null)
        {
            if (existing.UserId != userId)
            {
                _db.UserPushTokens.Remove(existing);
                existing = UserPushToken.Create(userId, token, request.Platform, request.DeviceName);
                _db.UserPushTokens.Add(existing);
            }
            else
            {
                existing.Touch(request.Platform, request.DeviceName);
            }
        }
        else
        {
            existing = UserPushToken.Create(userId, token, request.Platform, request.DeviceName);
            _db.UserPushTokens.Add(existing);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result<PushTokenResponse>.Success(new PushTokenResponse(
            existing.Id,
            existing.Token,
            existing.Platform,
            existing.DeviceName,
            existing.UpdatedAt));
    }

    public async Task<Result> UnregisterAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken = default)
    {
        var normalized = token?.Trim() ?? string.Empty;
        var existing = await _db.UserPushTokens
            .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == normalized, cancellationToken);

        if (existing is null)
        {
            return Result.Success();
        }

        _db.UserPushTokens.Remove(existing);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> UnregisterAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tokens = await _db.UserPushTokens
            .Where(t => t.UserId == userId)
            .ToListAsync(cancellationToken);

        if (tokens.Count == 0)
        {
            return Result.Success();
        }

        _db.UserPushTokens.RemoveRange(tokens);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

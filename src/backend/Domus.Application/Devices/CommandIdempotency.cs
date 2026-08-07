using Domus.Application.Abstractions;
using Domus.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Domus.Application.Devices;

/// <summary>
/// Helper for FASE 2 command creation. Guarantees idempotent behavior when client sends IdempotencyKey.
/// </summary>
public interface ICommandIdempotencyService
{
    Task<Command?> FindExistingAsync(Guid deviceId, string idempotencyKey, CancellationToken cancellationToken = default);
}

public sealed class CommandIdempotencyService : ICommandIdempotencyService
{
    private readonly IDomusDbContext _db;

    public CommandIdempotencyService(IDomusDbContext db)
    {
        _db = db;
    }

    public async Task<Command?> FindExistingAsync(
        Guid deviceId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return null;
        }

        return await _db.Commands
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.DeviceId == deviceId && c.IdempotencyKey == idempotencyKey,
                cancellationToken);
    }
}

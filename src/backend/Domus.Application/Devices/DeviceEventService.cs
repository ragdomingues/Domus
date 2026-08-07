using Domus.Application.Abstractions;
using Domus.Application.Common;
using Domus.Application.Security;
using Domus.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Domus.Application.Devices;

public sealed record DeviceEventResponse(
    Guid Id,
    Guid DeviceId,
    Guid? UserId,
    string? UserName,
    Guid? CommandId,
    string Action,
    string Result,
    string Origin,
    string? Details,
    DateTimeOffset CreatedAt);

public interface IDeviceEventService
{
    Task<Result<IReadOnlyList<DeviceEventResponse>>> ListByDeviceAsync(
        Guid deviceId,
        Guid userId,
        int take = 50,
        CancellationToken cancellationToken = default);
}

public sealed class DeviceEventService : IDeviceEventService
{
    private readonly IDomusDbContext _db;
    private readonly IAccessControlService _access;
    private readonly IDateTimeProvider _clock;
    private readonly HistoryRetentionOptions _retention;

    public DeviceEventService(
        IDomusDbContext db,
        IAccessControlService access,
        IDateTimeProvider clock,
        IOptions<HistoryRetentionOptions> retention)
    {
        _db = db;
        _access = access;
        _clock = clock;
        _retention = retention.Value;
    }

    public async Task<Result<IReadOnlyList<DeviceEventResponse>>> ListByDeviceAsync(
        Guid deviceId,
        Guid userId,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var access = await _access.EnsureCanAccessDeviceAsync(userId, deviceId, cancellationToken);
        if (!access.Succeeded)
        {
            return Result<IReadOnlyList<DeviceEventResponse>>.Failure(access.Error!, access.ErrorCode);
        }

        take = Math.Clamp(take, 1, 200);
        var cutoff = _clock.UtcNow.AddDays(-Math.Clamp(_retention.RetentionDays, 1, 3650));
        var rows = await (
            from e in _db.DeviceEvents.AsNoTracking()
            where e.DeviceId == deviceId && e.CreatedAt >= cutoff
            orderby e.CreatedAt descending
            join u in _db.Users.AsNoTracking() on e.UserId equals u.Id into uj
            from u in uj.DefaultIfEmpty()
            select new DeviceEventResponse(
                e.Id,
                e.DeviceId,
                e.UserId,
                u != null ? u.Name : null,
                e.CommandId,
                e.Action,
                e.Result.ToString(),
                e.Origin.ToString(),
                e.Details,
                e.CreatedAt)
        ).Take(take).ToListAsync(cancellationToken);

        return Result<IReadOnlyList<DeviceEventResponse>>.Success(rows);
    }
}

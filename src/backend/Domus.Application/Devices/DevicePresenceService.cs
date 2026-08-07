using Domus.Application.Abstractions;
using Domus.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Domus.Application.Devices;

public interface IDevicePresenceService
{
    Task MarkStaleDevicesOfflineAsync(CancellationToken cancellationToken = default);
}

public sealed class DevicePresenceService : IDevicePresenceService
{
    public const double HeartbeatMissFactor = 2.5;
    public static readonly TimeSpan MinimumOfflineGrace = TimeSpan.FromSeconds(90);

    private readonly IDomusDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly IDeviceRealtimeNotifier _realtime;
    private readonly ILogger<DevicePresenceService> _logger;

    public DevicePresenceService(
        IDomusDbContext db,
        IDateTimeProvider clock,
        IDeviceRealtimeNotifier realtime,
        ILogger<DevicePresenceService> logger)
    {
        _db = db;
        _clock = clock;
        _realtime = realtime;
        _logger = logger;
    }

    public async Task MarkStaleDevicesOfflineAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var online = await (
            from d in _db.Devices
            where d.ConnectionStatus == DeviceConnectionStatus.Online &&
                  d.LifecycleStatus == DeviceLifecycleStatus.Active &&
                  !d.IsSimulated
            join c in _db.DeviceConfigurations on d.Id equals c.DeviceId into cfg
            from c in cfg.DefaultIfEmpty()
            select new
            {
                Device = d,
                HeartbeatSeconds = c != null ? c.HeartbeatIntervalSeconds : 30
            }).ToListAsync(cancellationToken);

        var changed = new List<(Guid TenantId, Guid ResidenceId, Guid DeviceId, DateTimeOffset? LastSeenAt)>();

        foreach (var item in online)
        {
            var grace = TimeSpan.FromSeconds(item.HeartbeatSeconds * HeartbeatMissFactor);
            if (grace < MinimumOfflineGrace)
            {
                grace = MinimumOfflineGrace;
            }

            var lastSeen = item.Device.LastSeenAt ?? item.Device.CreatedAt;
            if (now - lastSeen < grace)
            {
                continue;
            }

            item.Device.MarkOffline();
            changed.Add((item.Device.TenantId, item.Device.ResidenceId, item.Device.Id, item.Device.LastSeenAt));
        }

        if (changed.Count == 0)
        {
            return;
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Marcou {Count} dispositivo(s) offline por ausência de heartbeat", changed.Count);

        foreach (var item in changed)
        {
            await _realtime.NotifyDeviceOfflineAsync(
                item.TenantId,
                item.ResidenceId,
                item.DeviceId,
                item.LastSeenAt,
                cancellationToken);

            await _realtime.NotifyDeviceStatusChangedAsync(
                item.TenantId,
                item.ResidenceId,
                item.DeviceId,
                DeviceConnectionStatus.Offline,
                null,
                now,
                cancellationToken);
        }
    }
}

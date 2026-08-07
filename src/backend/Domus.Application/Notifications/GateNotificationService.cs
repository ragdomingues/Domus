using System.Text.Json;
using Domus.Application.Abstractions;
using Domus.Domain.Entities;
using Domus.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Domus.Application.Notifications;

public interface IGateNotificationService
{
    Task NotifyGateStateChangedAsync(
        Device device,
        GateState newState,
        DateTimeOffset reportedAt,
        CancellationToken cancellationToken = default);

    Task ProcessOpenTooLongAlertsAsync(CancellationToken cancellationToken = default);
}

public sealed class GateNotificationService : IGateNotificationService
{
    public const string TypeOpened = "gate.opened";
    public const string TypeClosed = "gate.closed";
    public const string TypeOpenTooLong = "gate.open_too_long";

    private readonly IDomusDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly IDeviceRealtimeNotifier _realtime;
    private readonly IPushNotificationSender _push;
    private readonly ILogger<GateNotificationService> _logger;

    public GateNotificationService(
        IDomusDbContext db,
        IDateTimeProvider clock,
        IDeviceRealtimeNotifier realtime,
        IPushNotificationSender push,
        ILogger<GateNotificationService> logger)
    {
        _db = db;
        _clock = clock;
        _realtime = realtime;
        _push = push;
        _logger = logger;
    }

    public async Task NotifyGateStateChangedAsync(
        Device device,
        GateState newState,
        DateTimeOffset reportedAt,
        CancellationToken cancellationToken = default)
    {
        if (device.Type != DeviceType.Gate)
        {
            return;
        }

        if (newState is not (GateState.Open or GateState.Closed))
        {
            return;
        }

        if (newState == GateState.Open)
        {
            var tracked = await _db.UserDeviceNotificationPreferences
                .Where(p => p.DeviceId == device.Id && p.LastOpenAlertAt != null)
                .ToListAsync(cancellationToken);
            foreach (var pref in tracked)
            {
                pref.ClearOpenAlert();
            }

            if (tracked.Count > 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        var prefs = await LoadActivePreferencesAsync(device, cancellationToken);
        var targets = newState == GateState.Open
            ? prefs.Where(p => p.NotifyOnOpen).ToList()
            : prefs.Where(p => p.NotifyOnClose).ToList();

        if (targets.Count == 0)
        {
            return;
        }

        var type = newState == GateState.Open ? TypeOpened : TypeClosed;
        var title = newState == GateState.Open ? "Portão aberto" : "Portão fechado";
        var body = newState == GateState.Open
            ? $"{device.Name} foi aberto."
            : $"{device.Name} foi fechado.";

        await CreateAndNotifyAsync(device, targets.Select(p => p.UserId).ToList(), type, title, body, reportedAt, cancellationToken);
    }

    public async Task ProcessOpenTooLongAlertsAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var openGates = await _db.Gates
            .Include(g => g.Device)
            .Where(g =>
                g.GateState == GateState.Open &&
                g.OpenedAt != null &&
                g.Device != null &&
                g.Device.LifecycleStatus != DeviceLifecycleStatus.Deleted &&
                g.Device.LifecycleStatus != DeviceLifecycleStatus.Suspended)
            .ToListAsync(cancellationToken);

        foreach (var gate in openGates)
        {
            var device = gate.Device!;
            if (gate.OpenedAt is null)
            {
                continue;
            }

            var memberUserIds = await GetActiveMemberUserIdsAsync(device.ResidenceId, now, cancellationToken);
            if (memberUserIds.Count == 0)
            {
                continue;
            }

            var prefs = await _db.UserDeviceNotificationPreferences
                .Where(p =>
                    p.DeviceId == device.Id &&
                    p.NotifyWhenOpenTooLong &&
                    memberUserIds.Contains(p.UserId))
                .ToListAsync(cancellationToken);

            var due = prefs
                .Where(p =>
                {
                    var threshold = gate.OpenedAt.Value.AddMinutes(p.OpenAlertMinutes);
                    if (now < threshold)
                    {
                        return false;
                    }

                    return p.LastOpenAlertAt is null || p.LastOpenAlertAt < gate.OpenedAt;
                })
                .ToList();

            if (due.Count == 0)
            {
                continue;
            }

            var minutesOpen = (int)Math.Max(1, Math.Round((now - gate.OpenedAt.Value).TotalMinutes));
            var title = "Portão aberto há muito tempo";
            var body = $"{device.Name} está aberto há cerca de {minutesOpen} minuto(s).";

            await CreateAndNotifyAsync(
                device,
                due.Select(p => p.UserId).ToList(),
                TypeOpenTooLong,
                title,
                body,
                now,
                cancellationToken);

            foreach (var pref in due)
            {
                pref.MarkOpenAlertSent(now);
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<List<UserDeviceNotificationPreference>> LoadActivePreferencesAsync(
        Device device,
        CancellationToken cancellationToken)
    {
        var memberUserIds = await GetActiveMemberUserIdsAsync(device.ResidenceId, _clock.UtcNow, cancellationToken);
        if (memberUserIds.Count == 0)
        {
            return [];
        }

        return await _db.UserDeviceNotificationPreferences
            .AsNoTracking()
            .Where(p => p.DeviceId == device.Id && memberUserIds.Contains(p.UserId))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<Guid>> GetActiveMemberUserIdsAsync(
        Guid residenceId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        await _db.ResidenceMemberships.AsNoTracking()
            .Where(m =>
                m.ResidenceId == residenceId &&
                m.RevokedAt == null &&
                m.ValidFrom <= now &&
                (m.ValidUntil == null || m.ValidUntil >= now))
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);

    private async Task CreateAndNotifyAsync(
        Device device,
        IReadOnlyList<Guid> userIds,
        string type,
        string title,
        string body,
        DateTimeOffset reportedAt,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            deviceId = device.Id,
            residenceId = device.ResidenceId,
            deviceName = device.Name,
            type,
            reportedAt
        });

        var distinctUserIds = userIds.Distinct().ToList();
        var created = new List<(Guid UserId, Notification Notification, NotificationDelivery PushDelivery)>();
        foreach (var userId in distinctUserIds)
        {
            var notification = Notification.Create(
                device.TenantId,
                userId,
                type,
                title,
                body,
                payload);

            var inApp = NotificationDelivery.Create(notification.Id, "InApp");
            inApp.MarkSent(_clock.UtcNow);
            var pushDelivery = NotificationDelivery.Create(notification.Id, "Push");

            _db.Notifications.Add(notification);
            _db.NotificationDeliveries.Add(inApp);
            _db.NotificationDeliveries.Add(pushDelivery);
            created.Add((userId, notification, pushDelivery));
        }

        await _db.SaveChangesAsync(cancellationToken);

        foreach (var (userId, _, _) in created)
        {
            try
            {
                await _realtime.NotifyUserNotificationCreatedAsync(
                    userId,
                    type,
                    title,
                    body,
                    device.Id,
                    reportedAt,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao enviar SignalR de notificação para {UserId}", userId);
            }
        }

        await SendPushAsync(created, type, title, body, device, cancellationToken);
    }

    private async Task SendPushAsync(
        List<(Guid UserId, Notification Notification, NotificationDelivery PushDelivery)> created,
        string type,
        string title,
        string body,
        Device device,
        CancellationToken cancellationToken)
    {
        var userIds = created.Select(c => c.UserId).Distinct().ToList();
        var tokens = await _db.UserPushTokens
            .Where(t => userIds.Contains(t.UserId))
            .ToListAsync(cancellationToken);

        if (tokens.Count == 0)
        {
            foreach (var (_, _, pushDelivery) in created)
            {
                pushDelivery.MarkFailed("no_push_token");
            }

            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var tokensByUser = tokens.GroupBy(t => t.UserId).ToDictionary(g => g.Key, g => g.ToList());
        var messages = new List<PushNotificationMessage>();
        var messageMeta = new List<(UserPushToken Token, NotificationDelivery Delivery)>();

        foreach (var (userId, notification, pushDelivery) in created)
        {
            if (!tokensByUser.TryGetValue(userId, out var userTokens) || userTokens.Count == 0)
            {
                pushDelivery.MarkFailed("no_push_token");
                continue;
            }

            foreach (var token in userTokens)
            {
                messages.Add(new PushNotificationMessage(
                    token.Token,
                    title,
                    body,
                    new Dictionary<string, string>
                    {
                        ["type"] = type,
                        ["notificationId"] = notification.Id.ToString(),
                        ["deviceId"] = device.Id.ToString(),
                        ["residenceId"] = device.ResidenceId.ToString()
                    }));
                messageMeta.Add((token, pushDelivery));
            }
        }

        if (messages.Count == 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        var results = await _push.SendAsync(messages, cancellationToken);
        var staleTokens = new List<UserPushToken>();
        var deliverySuccess = new HashSet<Guid>();

        for (var i = 0; i < results.Count && i < messageMeta.Count; i++)
        {
            var result = results[i];
            var (token, delivery) = messageMeta[i];

            if (result.Succeeded)
            {
                deliverySuccess.Add(delivery.Id);
                token.MarkUsed(_clock.UtcNow);
                continue;
            }

            if (result.ShouldRemoveToken)
            {
                staleTokens.Add(token);
            }
        }

        foreach (var (_, _, pushDelivery) in created)
        {
            if (pushDelivery.Status != "Pending")
            {
                continue;
            }

            if (deliverySuccess.Contains(pushDelivery.Id))
            {
                pushDelivery.MarkSent(_clock.UtcNow);
            }
            else
            {
                var error = results
                    .FirstOrDefault(r => !r.Succeeded)?.ErrorMessage
                    ?? results.FirstOrDefault(r => !r.Succeeded)?.ErrorCode
                    ?? "push_failed";
                pushDelivery.MarkFailed(error);
            }
        }

        if (staleTokens.Count > 0)
        {
            foreach (var token in staleTokens.DistinctBy(t => t.Id))
            {
                _db.UserPushTokens.Remove(token);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}

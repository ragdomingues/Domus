using Domus.Api.Hubs;
using Domus.Application.Abstractions;
using Domus.Application.Realtime;
using Domus.Domain.Enums;
using Microsoft.AspNetCore.SignalR;

namespace Domus.Api.Realtime;

public sealed class SignalRDeviceRealtimeNotifier : IDeviceRealtimeNotifier
{
    private readonly IHubContext<DevicesHub> _hub;

    public SignalRDeviceRealtimeNotifier(IHubContext<DevicesHub> hub)
    {
        _hub = hub;
    }

    public Task NotifyDeviceStatusChangedAsync(
        Guid tenantId,
        Guid residenceId,
        Guid deviceId,
        DeviceConnectionStatus connectionStatus,
        GateState? gateState,
        DateTimeOffset reportedAt,
        CancellationToken cancellationToken = default) =>
        SendToAuthorizedGroupsAsync(
            tenantId,
            residenceId,
            DeviceRealtimeEventNames.DeviceStatusChanged,
            DeviceStatusChangedPayload.Create(deviceId, connectionStatus, gateState, reportedAt),
            cancellationToken);

    public Task NotifyGateStateChangedAsync(
        Guid tenantId,
        Guid residenceId,
        Guid deviceId,
        GateState gateState,
        DateTimeOffset reportedAt,
        CancellationToken cancellationToken = default) =>
        SendToAuthorizedGroupsAsync(
            tenantId,
            residenceId,
            DeviceRealtimeEventNames.GateStateChanged,
            GateStateChangedPayload.Create(deviceId, gateState, reportedAt),
            cancellationToken);

    public Task NotifyCommandUpdatedAsync(
        Guid tenantId,
        Guid residenceId,
        Guid commandId,
        Guid deviceId,
        CommandStatus status,
        CommandAction action,
        string? failureReason,
        CancellationToken cancellationToken = default) =>
        SendToAuthorizedGroupsAsync(
            tenantId,
            residenceId,
            DeviceRealtimeEventNames.CommandUpdated,
            CommandUpdatedPayload.Create(commandId, deviceId, status, action, failureReason),
            cancellationToken);

    public Task NotifyDeviceOfflineAsync(
        Guid tenantId,
        Guid residenceId,
        Guid deviceId,
        DateTimeOffset? lastSeenAt,
        CancellationToken cancellationToken = default) =>
        SendToAuthorizedGroupsAsync(
            tenantId,
            residenceId,
            DeviceRealtimeEventNames.DeviceOffline,
            DeviceOfflinePayload.Create(deviceId, lastSeenAt),
            cancellationToken);

    public Task NotifyUserNotificationCreatedAsync(
        Guid userId,
        string type,
        string title,
        string body,
        Guid? deviceId,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default) =>
        _hub.Clients.Group(DevicesHub.UserGroup(userId)).SendAsync(
            DeviceRealtimeEventNames.NotificationCreated,
            NotificationCreatedPayload.Create(type, title, body, deviceId, createdAt),
            cancellationToken);

    private async Task SendToAuthorizedGroupsAsync(
        Guid tenantId,
        Guid residenceId,
        string eventName,
        object payload,
        CancellationToken cancellationToken)
    {
        var groups = _hub.Clients.Groups(
        [
            DevicesHub.ResidenceGroup(residenceId),
            DevicesHub.TenantGroup(tenantId)
        ]);

        await groups.SendAsync(eventName, payload, cancellationToken);
    }
}

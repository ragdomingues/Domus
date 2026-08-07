using Domus.Domain.Enums;

namespace Domus.Application.Abstractions;

public interface IDeviceRealtimeNotifier
{
    Task NotifyDeviceStatusChangedAsync(
        Guid tenantId,
        Guid residenceId,
        Guid deviceId,
        DeviceConnectionStatus connectionStatus,
        GateState? gateState,
        DateTimeOffset reportedAt,
        CancellationToken cancellationToken = default);

    Task NotifyGateStateChangedAsync(
        Guid tenantId,
        Guid residenceId,
        Guid deviceId,
        GateState gateState,
        DateTimeOffset reportedAt,
        CancellationToken cancellationToken = default);

    Task NotifyCommandUpdatedAsync(
        Guid tenantId,
        Guid residenceId,
        Guid commandId,
        Guid deviceId,
        CommandStatus status,
        CommandAction action,
        string? failureReason,
        CancellationToken cancellationToken = default);

    Task NotifyDeviceOfflineAsync(
        Guid tenantId,
        Guid residenceId,
        Guid deviceId,
        DateTimeOffset? lastSeenAt,
        CancellationToken cancellationToken = default);

    Task NotifyUserNotificationCreatedAsync(
        Guid userId,
        string type,
        string title,
        string body,
        Guid? deviceId,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default);
}

public sealed class NullDeviceRealtimeNotifier : IDeviceRealtimeNotifier
{
    public static NullDeviceRealtimeNotifier Instance { get; } = new();

    public Task NotifyDeviceStatusChangedAsync(
        Guid tenantId,
        Guid residenceId,
        Guid deviceId,
        DeviceConnectionStatus connectionStatus,
        GateState? gateState,
        DateTimeOffset reportedAt,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task NotifyGateStateChangedAsync(
        Guid tenantId,
        Guid residenceId,
        Guid deviceId,
        GateState gateState,
        DateTimeOffset reportedAt,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task NotifyCommandUpdatedAsync(
        Guid tenantId,
        Guid residenceId,
        Guid commandId,
        Guid deviceId,
        CommandStatus status,
        CommandAction action,
        string? failureReason,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task NotifyDeviceOfflineAsync(
        Guid tenantId,
        Guid residenceId,
        Guid deviceId,
        DateTimeOffset? lastSeenAt,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task NotifyUserNotificationCreatedAsync(
        Guid userId,
        string type,
        string title,
        string body,
        Guid? deviceId,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public static class DeviceRealtimeEventNames
{
    public const string DeviceStatusChanged = "DeviceStatusChanged";
    public const string GateStateChanged = "GateStateChanged";
    public const string CommandUpdated = "CommandUpdated";
    public const string DeviceOffline = "DeviceOffline";
    public const string NotificationCreated = "NotificationCreated";
}

using Domus.Domain.Enums;

namespace Domus.Application.Realtime;

/// <summary>
/// Versão atual do envelope SignalR. Clientes devem ignorar eventos com schemaVersion desconhecida
/// ou tratar apenas campos conhecidos (forward-compatible).
/// </summary>
public static class RealtimeContract
{
    public const int SchemaVersion = 1;
}

public sealed record DeviceStatusChangedPayload(
    int SchemaVersion,
    Guid DeviceId,
    string ConnectionStatus,
    string? GateState,
    DateTimeOffset ReportedAt)
{
    public static DeviceStatusChangedPayload Create(
        Guid deviceId,
        DeviceConnectionStatus connectionStatus,
        GateState? gateState,
        DateTimeOffset reportedAt) =>
        new(
            RealtimeContract.SchemaVersion,
            deviceId,
            connectionStatus.ToString(),
            gateState?.ToString(),
            reportedAt);
}

public sealed record GateStateChangedPayload(
    int SchemaVersion,
    Guid DeviceId,
    string GateState,
    DateTimeOffset ReportedAt)
{
    public static GateStateChangedPayload Create(
        Guid deviceId,
        GateState gateState,
        DateTimeOffset reportedAt) =>
        new(RealtimeContract.SchemaVersion, deviceId, gateState.ToString(), reportedAt);
}

public sealed record CommandUpdatedPayload(
    int SchemaVersion,
    Guid CommandId,
    Guid DeviceId,
    string Status,
    string Action,
    string? FailureReason)
{
    public static CommandUpdatedPayload Create(
        Guid commandId,
        Guid deviceId,
        CommandStatus status,
        CommandAction action,
        string? failureReason) =>
        new(
            RealtimeContract.SchemaVersion,
            commandId,
            deviceId,
            status.ToString(),
            action.ToString(),
            failureReason);
}

public sealed record DeviceOfflinePayload(
    int SchemaVersion,
    Guid DeviceId,
    DateTimeOffset? LastSeenAt)
{
    public static DeviceOfflinePayload Create(Guid deviceId, DateTimeOffset? lastSeenAt) =>
        new(RealtimeContract.SchemaVersion, deviceId, lastSeenAt);
}

public sealed record NotificationCreatedPayload(
    int SchemaVersion,
    string Type,
    string Title,
    string Body,
    Guid? DeviceId,
    DateTimeOffset CreatedAt)
{
    public static NotificationCreatedPayload Create(
        string type,
        string title,
        string body,
        Guid? deviceId,
        DateTimeOffset createdAt) =>
        new(RealtimeContract.SchemaVersion, type, title, body, deviceId, createdAt);
}

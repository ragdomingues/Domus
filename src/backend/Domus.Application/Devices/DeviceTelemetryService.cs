using System.Text.Json;
using Domus.Application.Abstractions;
using Domus.Application.Notifications;
using Domus.Domain.Entities;
using Domus.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Domus.Application.Devices;

public interface IDeviceTelemetryService
{
    Task HandleIncomingAsync(string topic, string payload, CancellationToken cancellationToken = default);
}

public sealed class DeviceTelemetryService : IDeviceTelemetryService
{
    private readonly IDomusDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly IDeviceRealtimeNotifier _realtime;
    private readonly IGateNotificationService _gateNotifications;
    private readonly ILogger<DeviceTelemetryService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DeviceTelemetryService(
        IDomusDbContext db,
        IDateTimeProvider clock,
        IDeviceRealtimeNotifier realtime,
        IGateNotificationService gateNotifications,
        ILogger<DeviceTelemetryService> logger)
    {
        _db = db;
        _clock = clock;
        _realtime = realtime;
        _gateNotifications = gateNotifications;
        _logger = logger;
    }

    public async Task HandleIncomingAsync(string topic, string payload, CancellationToken cancellationToken = default)
    {
        if (!MqttTopics.TryParse(topic, out var tenantId, out var deviceId, out var leaf))
        {
            _logger.LogWarning("Tópico MQTT inválido: {Topic}", topic);
            return;
        }

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(payload) ? "{}" : payload);
        if (!doc.RootElement.TryGetProperty("messageId", out _) &&
            !doc.RootElement.TryGetProperty("MessageId", out _))
        {
            _logger.LogWarning("Mensagem MQTT sem messageId ignorada. Topic={Topic}", topic);
            return;
        }

        var device = await _db.Devices.FirstOrDefaultAsync(
            d => d.Id == deviceId && d.TenantId == tenantId,
            cancellationToken);

        if (device is null || device.LifecycleStatus is DeviceLifecycleStatus.Deleted or DeviceLifecycleStatus.Suspended)
        {
            _logger.LogWarning("Telemetry ignorada para device {DeviceId}", deviceId);
            return;
        }

        if (leaf.Equals("heartbeat", StringComparison.OrdinalIgnoreCase))
        {
            var heartbeat = JsonSerializer.Deserialize<HeartbeatPayload>(payload, JsonOptions);
            var wasOnline = device.ConnectionStatus == DeviceConnectionStatus.Online;
            var reportedAt = heartbeat?.ReportedAt ?? _clock.UtcNow;
            device.MarkOnline(heartbeat?.FirmwareVersion, reportedAt);
            await _db.SaveChangesAsync(cancellationToken);

            if (!wasOnline)
            {
                await _realtime.NotifyDeviceStatusChangedAsync(
                    device.TenantId,
                    device.ResidenceId,
                    device.Id,
                    device.ConnectionStatus,
                    null,
                    reportedAt,
                    cancellationToken);
            }

            return;
        }

        if (leaf.Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            var status = JsonSerializer.Deserialize<StatusPayload>(payload, JsonOptions);
            var wasOnline = device.ConnectionStatus == DeviceConnectionStatus.Online;
            var reportedAt = status?.ReportedAt ?? _clock.UtcNow;
            device.MarkOnline(null, reportedAt);

            GateState? gateStateChanged = null;
            if (device.Type == DeviceType.Gate && status?.State is not null)
            {
                var gate = await _db.Gates.FirstOrDefaultAsync(g => g.DeviceId == device.Id, cancellationToken);
                if (gate is not null && TryMapGateState(status.State, out var gateState) && gate.GateState != gateState)
                {
                    gate.UpdateState(gateState, reportedAt);
                    gateStateChanged = gateState;
                }
            }

            Command? updatedCommand = null;
            if (status?.CommandId is Guid commandId)
            {
                var command = await _db.Commands.FirstOrDefaultAsync(c => c.Id == commandId, cancellationToken);
                if (command is not null)
                {
                    var before = command.Status;
                    if (command.Status == CommandStatus.Pending)
                    {
                        command.MarkSent(_clock.UtcNow);
                    }

                    if (command.Status == CommandStatus.Sent)
                    {
                        command.MarkDelivered(_clock.UtcNow);
                    }

                    if (command.Status == CommandStatus.Delivered)
                    {
                        command.MarkExecuted(_clock.UtcNow);
                    }

                    if (command.Status != before)
                    {
                        updatedCommand = command;
                    }
                }
            }

            _db.DeviceEvents.Add(DeviceEvent.Create(
                tenantId,
                deviceId,
                action: $"STATUS_{status?.State ?? "UNKNOWN"}",
                result: EventResult.Success,
                origin: EventOrigin.System,
                commandId: status?.CommandId,
                details: payload.Length > 500 ? payload[..500] : payload));

            await _db.SaveChangesAsync(cancellationToken);

            GateState? currentGate = null;
            if (device.Type == DeviceType.Gate)
            {
                currentGate = await _db.Gates.AsNoTracking()
                    .Where(g => g.DeviceId == device.Id)
                    .Select(g => (GateState?)g.GateState)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (!wasOnline || gateStateChanged is not null)
            {
                await _realtime.NotifyDeviceStatusChangedAsync(
                    device.TenantId,
                    device.ResidenceId,
                    device.Id,
                    device.ConnectionStatus,
                    currentGate,
                    reportedAt,
                    cancellationToken);
            }

            if (gateStateChanged is not null)
            {
                await _realtime.NotifyGateStateChangedAsync(
                    device.TenantId,
                    device.ResidenceId,
                    device.Id,
                    gateStateChanged.Value,
                    reportedAt,
                    cancellationToken);

                await _gateNotifications.NotifyGateStateChangedAsync(
                    device,
                    gateStateChanged.Value,
                    reportedAt,
                    cancellationToken);
            }

            if (updatedCommand is not null)
            {
                await _realtime.NotifyCommandUpdatedAsync(
                    updatedCommand.TenantId,
                    device.ResidenceId,
                    updatedCommand.Id,
                    updatedCommand.DeviceId,
                    updatedCommand.Status,
                    updatedCommand.Action,
                    updatedCommand.FailureReason,
                    cancellationToken);
            }
        }
    }

    private static bool TryMapGateState(string state, out GateState gateState)
    {
        gateState = state.ToUpperInvariant() switch
        {
            "OPEN" => GateState.Open,
            "CLOSED" => GateState.Closed,
            "MOVING" => GateState.Moving,
            "UNKNOWN" => GateState.Unknown,
            _ => GateState.Unknown
        };
        return state.ToUpperInvariant() is "OPEN" or "CLOSED" or "MOVING" or "UNKNOWN";
    }

    private sealed record HeartbeatPayload(string? FirmwareVersion, int? UptimeSeconds, DateTimeOffset? ReportedAt);
    private sealed record StatusPayload(string? State, Guid? CommandId, DateTimeOffset? ReportedAt);
}

using Domus.Application.Abstractions;
using Domus.Application.Common;
using Domus.Application.Notifications;
using Domus.Application.Security;
using Domus.Domain.Entities;
using Domus.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Domus.Application.Devices;

public sealed record DisableSimulationResponse(
    DeviceResponse Device,
    Guid ProvisioningId,
    string ProvisioningCode,
    DateTimeOffset ExpiresAt);

public interface IDeviceSimulationService
{
    Task<Result<DeviceResponse>> EnableAsync(
        Guid deviceId,
        DeviceActorContext context,
        CancellationToken cancellationToken = default);

    Task<Result<DisableSimulationResponse>> DisableAsync(
        Guid deviceId,
        DeviceActorContext context,
        int? expiresInMinutes = 30,
        CancellationToken cancellationToken = default);

    Task CompleteCommandAsync(
        Guid commandId,
        CancellationToken cancellationToken = default);
}

public sealed class DeviceSimulationService : IDeviceSimulationService
{
    private readonly IDomusDbContext _db;
    private readonly IAccessControlService _access;
    private readonly ISecureTokenGenerator _tokens;
    private readonly ISecretHasher _hasher;
    private readonly IDateTimeProvider _clock;
    private readonly IDeviceRealtimeNotifier _realtime;
    private readonly IGateNotificationService _gateNotifications;
    private readonly IProvisioningService _provisioning;
    private readonly ILogger<DeviceSimulationService> _logger;

    public DeviceSimulationService(
        IDomusDbContext db,
        IAccessControlService access,
        ISecureTokenGenerator tokens,
        ISecretHasher hasher,
        IDateTimeProvider clock,
        IDeviceRealtimeNotifier realtime,
        IGateNotificationService gateNotifications,
        IProvisioningService provisioning,
        ILogger<DeviceSimulationService> logger)
    {
        _db = db;
        _access = access;
        _tokens = tokens;
        _hasher = hasher;
        _clock = clock;
        _realtime = realtime;
        _gateNotifications = gateNotifications;
        _provisioning = provisioning;
        _logger = logger;
    }

    public async Task<Result<DeviceResponse>> EnableAsync(
        Guid deviceId,
        DeviceActorContext context,
        CancellationToken cancellationToken = default)
    {
        var access = await _access.EnsureCanManageDeviceAsync(context.UserId, deviceId, cancellationToken);
        if (!access.Succeeded)
        {
            return Result<DeviceResponse>.Failure(access.Error!, access.ErrorCode);
        }

        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken);
        if (device is null)
        {
            return Result<DeviceResponse>.Failure("Dispositivo não encontrado.", "not_found");
        }

        if (device.Type != DeviceType.Gate)
        {
            return Result<DeviceResponse>.Failure(
                "Simulação disponível apenas para portões.",
                "invalid_device_type");
        }

        if (device.IsSimulated)
        {
            return Result<DeviceResponse>.Success(await MapDeviceAsync(device, cancellationToken));
        }

        try
        {
            var username = device.MqttUsername ?? _tokens.GenerateMqttUsername(device.Id);
            var secretHash = device.MqttSecretHash ?? _hasher.Hash(_tokens.GenerateMqttSecret());
            device.EnableSimulation(username, secretHash);
        }
        catch (InvalidOperationException ex)
        {
            return Result<DeviceResponse>.Failure(ex.Message, "invalid_state");
        }

        var gate = await _db.Gates.FirstOrDefaultAsync(g => g.DeviceId == device.Id, cancellationToken);
        if (gate is null)
        {
            gate = Gate.Create(device.Id);
            _db.Gates.Add(gate);
        }

        if (gate.GateState is GateState.Unknown or GateState.Moving)
        {
            gate.UpdateState(GateState.Closed, _clock.UtcNow);
        }

        if (string.IsNullOrWhiteSpace(device.HardwareId))
        {
            device.SetHardwareId($"sim-{device.Id:N}");
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _realtime.NotifyDeviceStatusChangedAsync(
            device.TenantId,
            device.ResidenceId,
            device.Id,
            device.ConnectionStatus,
            gate.GateState,
            _clock.UtcNow,
            cancellationToken);

        await _realtime.NotifyGateStateChangedAsync(
            device.TenantId,
            device.ResidenceId,
            device.Id,
            gate.GateState,
            _clock.UtcNow,
            cancellationToken);

        _logger.LogInformation("Simulação ativada para device {DeviceId}", device.Id);

        return Result<DeviceResponse>.Success(await MapDeviceAsync(device, cancellationToken));
    }

    public async Task<Result<DisableSimulationResponse>> DisableAsync(
        Guid deviceId,
        DeviceActorContext context,
        int? expiresInMinutes = 30,
        CancellationToken cancellationToken = default)
    {
        var access = await _access.EnsureCanManageDeviceAsync(context.UserId, deviceId, cancellationToken);
        if (!access.Succeeded)
        {
            return Result<DisableSimulationResponse>.Failure(access.Error!, access.ErrorCode);
        }

        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken);
        if (device is null)
        {
            return Result<DisableSimulationResponse>.Failure("Dispositivo não encontrado.", "not_found");
        }

        if (!device.IsSimulated)
        {
            return Result<DisableSimulationResponse>.Failure(
                "Dispositivo não está em modo demonstração.",
                "not_simulated");
        }

        try
        {
            device.DisableSimulation();
        }
        catch (InvalidOperationException ex)
        {
            return Result<DisableSimulationResponse>.Failure(ex.Message, "invalid_state");
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _realtime.NotifyDeviceStatusChangedAsync(
            device.TenantId,
            device.ResidenceId,
            device.Id,
            device.ConnectionStatus,
            null,
            _clock.UtcNow,
            cancellationToken);

        var issued = await _provisioning.IssueAsync(
            new IssueProvisioningRequest(deviceId, expiresInMinutes ?? 30),
            context,
            cancellationToken);

        if (!issued.Succeeded)
        {
            return Result<DisableSimulationResponse>.Failure(issued.Error!, issued.ErrorCode);
        }

        // Recarrega após o provisioning (lifecycle passa a Provisioning)
        device = await _db.Devices.FirstAsync(d => d.Id == deviceId, cancellationToken);
        var mapped = await MapDeviceAsync(device, cancellationToken);

        _logger.LogInformation(
            "Simulação desativada para device {DeviceId}; provisioning {ProvisioningId} emitido",
            device.Id,
            issued.Value!.ProvisioningId);

        return Result<DisableSimulationResponse>.Success(new DisableSimulationResponse(
            mapped,
            issued.Value.ProvisioningId,
            issued.Value.ProvisioningCode,
            issued.Value.ExpiresAt));
    }

    private async Task<DeviceResponse> MapDeviceAsync(Device device, CancellationToken cancellationToken)
    {
        var config = await _db.DeviceConfigurations.AsNoTracking()
            .FirstOrDefaultAsync(c => c.DeviceId == device.Id, cancellationToken);
        var gate = await _db.Gates.AsNoTracking()
            .FirstOrDefaultAsync(g => g.DeviceId == device.Id, cancellationToken);

        return new DeviceResponse(
            device.Id,
            device.TenantId,
            device.ResidenceId,
            device.Type,
            device.Name,
            device.LifecycleStatus,
            device.ConnectionStatus,
            device.FirmwareVersion,
            device.HardwareId,
            device.HasMqttCredentials,
            device.LastSeenAt,
            device.CreatedAt,
            config is null
                ? null
                : new DeviceConfigurationResponse(
                    config.RelayPulseMs,
                    config.HeartbeatIntervalSeconds,
                    config.CommandTimeoutSeconds,
                    config.OpenAlertMinutes,
                    config.SupportsClose,
                    config.SupportsStop,
                    config.CapabilitiesJson,
                    config.UpdatedAt),
            gate?.GateState,
            device.IsSimulated);
    }

    public async Task CompleteCommandAsync(Guid commandId, CancellationToken cancellationToken = default)
    {
        var command = await _db.Commands.FirstOrDefaultAsync(c => c.Id == commandId, cancellationToken);
        if (command is null)
        {
            return;
        }

        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == command.DeviceId, cancellationToken);
        if (device is null || !device.IsSimulated)
        {
            return;
        }

        var now = _clock.UtcNow;
        if (command.Status == CommandStatus.Pending)
        {
            command.MarkSent(now);
        }

        if (command.Status == CommandStatus.Sent)
        {
            command.MarkDelivered(now);
        }

        if (command.Status == CommandStatus.Delivered)
        {
            command.MarkExecuted(now);
        }

        device.MarkOnline("sim-1.0.0", now);

        GateState? newGateState = null;
        if (device.Type == DeviceType.Gate)
        {
            var gate = await _db.Gates.FirstOrDefaultAsync(g => g.DeviceId == device.Id, cancellationToken);
            if (gate is not null)
            {
                newGateState = command.Action switch
                {
                    CommandAction.Open => GateState.Open,
                    CommandAction.Close => GateState.Closed,
                    _ => gate.GateState
                };

                if (newGateState != gate.GateState)
                {
                    gate.UpdateState(newGateState.Value, now);
                }
            }
        }

        _db.DeviceEvents.Add(DeviceEvent.Create(
            device.TenantId,
            device.Id,
            action: $"STATUS_{(newGateState ?? GateState.Unknown).ToString().ToUpperInvariant()}",
            result: EventResult.Success,
            origin: EventOrigin.System,
            commandId: command.Id,
            details: "simulated"));

        await _db.SaveChangesAsync(cancellationToken);

        await _realtime.NotifyCommandUpdatedAsync(
            command.TenantId,
            device.ResidenceId,
            command.Id,
            command.DeviceId,
            command.Status,
            command.Action,
            command.FailureReason,
            cancellationToken);

        await _realtime.NotifyDeviceStatusChangedAsync(
            device.TenantId,
            device.ResidenceId,
            device.Id,
            device.ConnectionStatus,
            newGateState,
            now,
            cancellationToken);

        if (newGateState is not null)
        {
            await _realtime.NotifyGateStateChangedAsync(
                device.TenantId,
                device.ResidenceId,
                device.Id,
                newGateState.Value,
                now,
                cancellationToken);

            await _gateNotifications.NotifyGateStateChangedAsync(
                device,
                newGateState.Value,
                now,
                cancellationToken);
        }
    }
}

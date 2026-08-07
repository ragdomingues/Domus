using System.Text.Json;
using Domus.Application.Abstractions;
using Domus.Application.Common;
using Domus.Application.Security;
using Domus.Domain.Entities;
using Domus.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Domus.Application.Devices;

public interface IDeviceService
{
    Task<Result<DeviceResponse>> CreateAsync(CreateDeviceRequest request, DeviceActorContext context, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<DeviceResponse>>> ListByResidenceAsync(Guid residenceId, Guid userId, CancellationToken cancellationToken = default);
    Task<Result<DeviceResponse>> GetAsync(Guid deviceId, Guid userId, CancellationToken cancellationToken = default);
    Task<Result<DeviceResponse>> UpdateAsync(Guid deviceId, UpdateDeviceRequest request, DeviceActorContext context, CancellationToken cancellationToken = default);
    Task<Result<DeviceConfigurationResponse>> UpdateConfigurationAsync(Guid deviceId, DeviceConfigurationRequest request, DeviceActorContext context, CancellationToken cancellationToken = default);
    Task<Result> SoftDeleteAsync(Guid deviceId, DeviceActorContext context, CancellationToken cancellationToken = default);
}

public sealed class CreateDeviceRequestValidator : AbstractValidator<CreateDeviceRequest>
{
    public CreateDeviceRequestValidator()
    {
        RuleFor(x => x.ResidenceId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Type).IsInEnum();
    }
}

public sealed class UpdateDeviceRequestValidator : AbstractValidator<UpdateDeviceRequest>
{
    public UpdateDeviceRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class DeviceService : IDeviceService
{
    private readonly IDomusDbContext _db;
    private readonly IAccessControlService _access;
    private readonly IDeviceMessenger _messenger;
    private readonly IValidator<CreateDeviceRequest> _createValidator;
    private readonly IValidator<UpdateDeviceRequest> _updateValidator;
    private readonly ILogger<DeviceService> _logger;

    public DeviceService(
        IDomusDbContext db,
        IAccessControlService access,
        IDeviceMessenger messenger,
        IValidator<CreateDeviceRequest> createValidator,
        IValidator<UpdateDeviceRequest> updateValidator,
        ILogger<DeviceService> logger)
    {
        _db = db;
        _access = access;
        _messenger = messenger;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _logger = logger;
    }

    public async Task<Result<DeviceResponse>> CreateAsync(
        CreateDeviceRequest request,
        DeviceActorContext context,
        CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<DeviceResponse>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), "validation_error");
        }

        var manage = await _access.EnsureCanManageResidenceAsync(context.UserId, request.ResidenceId, cancellationToken);
        if (!manage.Succeeded)
        {
            return Result<DeviceResponse>.Failure(manage.Error!, manage.ErrorCode);
        }

        var residence = await _db.Residences.AsNoTracking().FirstAsync(r => r.Id == request.ResidenceId, cancellationToken);
        var device = Device.Create(residence.TenantId, residence.Id, request.Type, request.Name);
        var configuration = DeviceConfiguration.CreateDefault(device.Id);

        if (request.Configuration is not null)
        {
            ApplyConfiguration(configuration, request.Configuration, context.UserId);
        }

        _db.Devices.Add(device);
        _db.DeviceConfigurations.Add(configuration);

        if (request.Type == DeviceType.Gate)
        {
            _db.Gates.Add(Gate.Create(
                device.Id,
                supportsClose: configuration.SupportsClose,
                supportsStop: configuration.SupportsStop));
        }

        _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
            SecurityAuditAction.DeviceCreated,
            true,
            context.UserId,
            residence.TenantId,
            context.IpAddress,
            context.UserAgent,
            $"device:{device.Id};type:{device.Type}"));

        await _db.SaveChangesAsync(cancellationToken);
        return Result<DeviceResponse>.Success(await MapAsync(device.Id, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<DeviceResponse>>> ListByResidenceAsync(
        Guid residenceId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var access = await _access.EnsureCanAccessResidenceAsync(userId, residenceId, cancellationToken);
        if (!access.Succeeded)
        {
            return Result<IReadOnlyList<DeviceResponse>>.Failure(access.Error!, access.ErrorCode);
        }

        var devices = await _db.Devices
            .AsNoTracking()
            .Where(d => d.ResidenceId == residenceId)
            .OrderBy(d => d.Name)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        var result = new List<DeviceResponse>();
        foreach (var id in devices)
        {
            result.Add(await MapAsync(id, cancellationToken));
        }

        return Result<IReadOnlyList<DeviceResponse>>.Success(result);
    }

    public async Task<Result<DeviceResponse>> GetAsync(
        Guid deviceId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var access = await _access.EnsureCanAccessDeviceAsync(userId, deviceId, cancellationToken);
        if (!access.Succeeded)
        {
            return Result<DeviceResponse>.Failure(access.Error!, access.ErrorCode);
        }

        return Result<DeviceResponse>.Success(await MapAsync(deviceId, cancellationToken));
    }

    public async Task<Result<DeviceResponse>> UpdateAsync(
        Guid deviceId,
        UpdateDeviceRequest request,
        DeviceActorContext context,
        CancellationToken cancellationToken = default)
    {
        var validation = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<DeviceResponse>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), "validation_error");
        }

        var manage = await _access.EnsureCanManageDeviceAsync(context.UserId, deviceId, cancellationToken);
        if (!manage.Succeeded)
        {
            return Result<DeviceResponse>.Failure(manage.Error!, manage.ErrorCode);
        }

        var device = await _db.Devices.FirstAsync(d => d.Id == deviceId, cancellationToken);
        device.Rename(request.Name);

        _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
            SecurityAuditAction.DeviceUpdated,
            true,
            context.UserId,
            device.TenantId,
            context.IpAddress,
            context.UserAgent,
            $"device:{device.Id}"));

        await _db.SaveChangesAsync(cancellationToken);
        return Result<DeviceResponse>.Success(await MapAsync(device.Id, cancellationToken));
    }

    public async Task<Result<DeviceConfigurationResponse>> UpdateConfigurationAsync(
        Guid deviceId,
        DeviceConfigurationRequest request,
        DeviceActorContext context,
        CancellationToken cancellationToken = default)
    {
        var manage = await _access.EnsureCanManageDeviceAsync(context.UserId, deviceId, cancellationToken);
        if (!manage.Succeeded)
        {
            return Result<DeviceConfigurationResponse>.Failure(manage.Error!, manage.ErrorCode);
        }

        var configuration = await _db.DeviceConfigurations.FirstOrDefaultAsync(c => c.DeviceId == deviceId, cancellationToken);
        if (configuration is null)
        {
            configuration = DeviceConfiguration.CreateDefault(deviceId);
            _db.DeviceConfigurations.Add(configuration);
        }

        ApplyConfiguration(configuration, request, context.UserId);

        var device = await _db.Devices.AsNoTracking().FirstAsync(d => d.Id == deviceId, cancellationToken);
        _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
            SecurityAuditAction.DeviceUpdated,
            true,
            context.UserId,
            device.TenantId,
            context.IpAddress,
            context.UserAgent,
            $"device_config:{deviceId}"));

        await _db.SaveChangesAsync(cancellationToken);

        if (device.HasMqttCredentials)
        {
            try
            {
                // Config operacional fica retained; otaUrl NÃO — evita loop OTA após reboot.
                var payload = JsonSerializer.Serialize(new
                {
                    relayPulseMs = configuration.RelayPulseMs,
                    heartbeatIntervalSeconds = configuration.HeartbeatIntervalSeconds,
                    commandTimeoutSeconds = configuration.CommandTimeoutSeconds,
                    supportsClose = configuration.SupportsClose,
                    supportsStop = configuration.SupportsStop,
                });
                await _messenger.PublishConfigurationAsync(
                    device.TenantId,
                    device.Id,
                    payload,
                    cancellationToken,
                    retain: true);

                if (!string.IsNullOrWhiteSpace(request.OtaUrl))
                {
                    var otaPayload = JsonSerializer.Serialize(new { otaUrl = request.OtaUrl.Trim() });
                    await _messenger.PublishConfigurationAsync(
                        device.TenantId,
                        device.Id,
                        otaPayload,
                        cancellationToken,
                        retain: false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Config salva no banco, mas falhou publish MQTT para device {DeviceId}",
                    deviceId);
            }
        }

        return Result<DeviceConfigurationResponse>.Success(MapConfig(configuration));
    }

    public async Task<Result> SoftDeleteAsync(
        Guid deviceId,
        DeviceActorContext context,
        CancellationToken cancellationToken = default)
    {
        var manage = await _access.EnsureCanManageDeviceAsync(context.UserId, deviceId, cancellationToken);
        if (!manage.Succeeded)
        {
            return manage;
        }

        var device = await _db.Devices.FirstAsync(d => d.Id == deviceId, cancellationToken);
        device.SoftDelete(context.UserId);

        var pending = await _db.DeviceProvisionings
            .Where(p => p.DeviceId == deviceId && p.Status == ProvisioningStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var item in pending)
        {
            item.Revoke();
        }

        _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
            SecurityAuditAction.DeviceDeleted,
            true,
            context.UserId,
            device.TenantId,
            context.IpAddress,
            context.UserAgent,
            $"device:{device.Id}"));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<DeviceResponse> MapAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var device = await _db.Devices.AsNoTracking().FirstAsync(d => d.Id == deviceId, cancellationToken);
        var config = await _db.DeviceConfigurations.AsNoTracking().FirstOrDefaultAsync(c => c.DeviceId == deviceId, cancellationToken);
        var gate = await _db.Gates.AsNoTracking().FirstOrDefaultAsync(g => g.DeviceId == deviceId, cancellationToken);

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
            config is null ? null : MapConfig(config),
            gate?.GateState,
            device.IsSimulated);
    }

    private static DeviceConfigurationResponse MapConfig(DeviceConfiguration configuration) =>
        new(
            configuration.RelayPulseMs,
            configuration.HeartbeatIntervalSeconds,
            configuration.CommandTimeoutSeconds,
            configuration.OpenAlertMinutes,
            configuration.SupportsClose,
            configuration.SupportsStop,
            configuration.CapabilitiesJson,
            configuration.UpdatedAt);

    private static void ApplyConfiguration(
        DeviceConfiguration configuration,
        DeviceConfigurationRequest request,
        Guid userId)
    {
        configuration.Update(
            request.RelayPulseMs,
            request.HeartbeatIntervalSeconds,
            request.CommandTimeoutSeconds,
            request.OpenAlertMinutes,
            request.SupportsClose,
            request.SupportsStop,
            string.IsNullOrWhiteSpace(request.CapabilitiesJson) ? "{}" : request.CapabilitiesJson,
            userId);
    }
}

using Domus.Application.Abstractions;
using Domus.Application.Common;
using Domus.Application.Security;
using Domus.Domain.Entities;
using Domus.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Domus.Application.Devices;

public sealed record IssueProvisioningRequest(Guid DeviceId, int? ExpiresInMinutes = 60);

public sealed record IssueProvisioningResponse(
    Guid ProvisioningId,
    Guid DeviceId,
    string ProvisioningCode,
    DateTimeOffset ExpiresAt);

public sealed record ActivateProvisioningRequest(
    string ProvisioningCode,
    string HardwareId,
    string? FirmwareVersion = null);

/// <summary>
/// One-time MQTT credentials returned ONLY on successful activate.
/// Never returned again by GET endpoints.
/// </summary>
public sealed record ActivateProvisioningResponse(
    Guid DeviceId,
    Guid TenantId,
    string MqttUsername,
    string MqttPassword,
    string TopicCommand,
    string TopicStatus,
    string TopicHeartbeat,
    string TopicConfig);

public sealed record ProvisioningStatusResponse(
    Guid Id,
    Guid DeviceId,
    ProvisioningStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? ActivatedAt);

public interface IProvisioningService
{
    Task<Result<IssueProvisioningResponse>> IssueAsync(IssueProvisioningRequest request, DeviceActorContext context, CancellationToken cancellationToken = default);
    Task<Result<ActivateProvisioningResponse>> ActivateAsync(ActivateProvisioningRequest request, string? ipAddress, CancellationToken cancellationToken = default);
    Task<Result<ProvisioningStatusResponse>> GetStatusAsync(Guid provisioningId, Guid userId, CancellationToken cancellationToken = default);
}

public sealed class IssueProvisioningRequestValidator : AbstractValidator<IssueProvisioningRequest>
{
    public IssueProvisioningRequestValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty();
        RuleFor(x => x.ExpiresInMinutes).GreaterThan(0).LessThanOrEqualTo(24 * 60).When(x => x.ExpiresInMinutes.HasValue);
    }
}

public sealed class ActivateProvisioningRequestValidator : AbstractValidator<ActivateProvisioningRequest>
{
    public ActivateProvisioningRequestValidator()
    {
        RuleFor(x => x.ProvisioningCode).NotEmpty().MaximumLength(200);
        RuleFor(x => x.HardwareId).NotEmpty().MaximumLength(128);
        RuleFor(x => x.FirmwareVersion).MaximumLength(64);
    }
}

public sealed class ProvisioningService : IProvisioningService
{
    private readonly IDomusDbContext _db;
    private readonly IAccessControlService _access;
    private readonly ISecretHasher _secretHasher;
    private readonly ISecureTokenGenerator _tokenGenerator;
    private readonly IDateTimeProvider _clock;
    private readonly IActivateAbuseGuard _abuseGuard;
    private readonly IValidator<IssueProvisioningRequest> _issueValidator;
    private readonly IValidator<ActivateProvisioningRequest> _activateValidator;

    public ProvisioningService(
        IDomusDbContext db,
        IAccessControlService access,
        ISecretHasher secretHasher,
        ISecureTokenGenerator tokenGenerator,
        IDateTimeProvider clock,
        IActivateAbuseGuard abuseGuard,
        IValidator<IssueProvisioningRequest> issueValidator,
        IValidator<ActivateProvisioningRequest> activateValidator)
    {
        _db = db;
        _access = access;
        _secretHasher = secretHasher;
        _tokenGenerator = tokenGenerator;
        _clock = clock;
        _abuseGuard = abuseGuard;
        _issueValidator = issueValidator;
        _activateValidator = activateValidator;
    }

    public async Task<Result<IssueProvisioningResponse>> IssueAsync(
        IssueProvisioningRequest request,
        DeviceActorContext context,
        CancellationToken cancellationToken = default)
    {
        var validation = await _issueValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<IssueProvisioningResponse>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), "validation_error");
        }

        var manage = await _access.EnsureCanManageDeviceAsync(context.UserId, request.DeviceId, cancellationToken);
        if (!manage.Succeeded)
        {
            return Result<IssueProvisioningResponse>.Failure(manage.Error!, manage.ErrorCode);
        }

        var device = await _db.Devices.FirstAsync(d => d.Id == request.DeviceId, cancellationToken);
        if (device.HasMqttCredentials)
        {
            return Result<IssueProvisioningResponse>.Failure("Dispositivo já ativado.", "device_already_activated");
        }

        var pending = await _db.DeviceProvisionings
            .Where(p => p.DeviceId == device.Id && p.Status == ProvisioningStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var item in pending)
        {
            item.Revoke();
        }

        device.MarkProvisioning();

        var plainCode = _tokenGenerator.GenerateProvisioningCode();
        var expiresAt = _clock.UtcNow.AddMinutes(request.ExpiresInMinutes ?? 60);
        var provisioning = DeviceProvisioning.Create(
            device.Id,
            device.TenantId,
            _secretHasher.Hash(plainCode),
            expiresAt);

        _db.DeviceProvisionings.Add(provisioning);
        _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
            SecurityAuditAction.ProvisioningIssued,
            true,
            context.UserId,
            device.TenantId,
            context.IpAddress,
            context.UserAgent,
            $"device:{device.Id};provisioning:{provisioning.Id}"));

        await _db.SaveChangesAsync(cancellationToken);

        return Result<IssueProvisioningResponse>.Success(new IssueProvisioningResponse(
            provisioning.Id,
            device.Id,
            plainCode,
            expiresAt));
    }

    public async Task<Result<ActivateProvisioningResponse>> ActivateAsync(
        ActivateProvisioningRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var validation = await _activateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<ActivateProvisioningResponse>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), "validation_error");
        }

        var abuse = _abuseGuard.EnsureAllowed(ipAddress, request.ProvisioningCode, request.HardwareId);
        if (!abuse.Succeeded)
        {
            return Result<ActivateProvisioningResponse>.Failure(abuse.Error!, abuse.ErrorCode);
        }

        var codeHash = _secretHasher.Hash(request.ProvisioningCode);
        var provisioning = await _db.DeviceProvisionings
            .FirstOrDefaultAsync(p => p.ProvisioningCodeHash == codeHash, cancellationToken);

        if (provisioning is null)
        {
            _abuseGuard.RegisterFailure(ipAddress, request.ProvisioningCode, request.HardwareId);
            _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
                SecurityAuditAction.ProvisioningFailed,
                false,
                details: "unknown_code",
                ipAddress: ipAddress));
            await _db.SaveChangesAsync(cancellationToken);
            return Result<ActivateProvisioningResponse>.Failure("Código de provisioning inválido.", "invalid_provisioning_code");
        }

        if (provisioning.Status == ProvisioningStatus.Activated)
        {
            _abuseGuard.RegisterFailure(ipAddress, request.ProvisioningCode, request.HardwareId);
            _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
                SecurityAuditAction.ProvisioningFailed,
                false,
                tenantId: provisioning.TenantId,
                details: $"reuse:{provisioning.Id}",
                ipAddress: ipAddress));
            await _db.SaveChangesAsync(cancellationToken);
            return Result<ActivateProvisioningResponse>.Failure("Provisioning já utilizado.", "provisioning_reused");
        }

        if (provisioning.Status is ProvisioningStatus.Revoked or ProvisioningStatus.Expired
            || !provisioning.CanActivate(_clock.UtcNow))
        {
            if (provisioning.Status == ProvisioningStatus.Pending && _clock.UtcNow > provisioning.ExpiresAt)
            {
                provisioning.MarkExpired();
            }

            _abuseGuard.RegisterFailure(ipAddress, request.ProvisioningCode, request.HardwareId);
            _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
                SecurityAuditAction.ProvisioningFailed,
                false,
                tenantId: provisioning.TenantId,
                details: $"expired_or_invalid:{provisioning.Id};status:{provisioning.Status}",
                ipAddress: ipAddress));
            await _db.SaveChangesAsync(cancellationToken);
            return Result<ActivateProvisioningResponse>.Failure("Provisioning expirado ou inválido.", "provisioning_expired");
        }

        var device = await _db.Devices.FirstAsync(d => d.Id == provisioning.DeviceId, cancellationToken);
        if (device.HasMqttCredentials || device.LifecycleStatus == DeviceLifecycleStatus.Active)
        {
            provisioning.Revoke();
            _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
                SecurityAuditAction.ProvisioningFailed,
                false,
                tenantId: device.TenantId,
                details: $"device_already_activated:{device.Id}",
                ipAddress: ipAddress));
            await _db.SaveChangesAsync(cancellationToken);
            return Result<ActivateProvisioningResponse>.Failure("Dispositivo já ativado.", "device_already_activated");
        }

        var hardwareId = request.HardwareId.Trim();
        var hardwareTaken = await _db.Devices.AnyAsync(
            d => d.HardwareId == hardwareId && d.Id != device.Id,
            cancellationToken);
        if (hardwareTaken)
        {
            _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
                SecurityAuditAction.ProvisioningFailed,
                false,
                tenantId: device.TenantId,
                details: "hardware_id_conflict",
                ipAddress: ipAddress));
            await _db.SaveChangesAsync(cancellationToken);
            return Result<ActivateProvisioningResponse>.Failure("HardwareId já vinculado a outro dispositivo.", "hardware_id_conflict");
        }

        var mqttUsername = _tokenGenerator.GenerateMqttUsername(device.Id);
        var mqttPassword = _tokenGenerator.GenerateMqttSecret();

        try
        {
            provisioning.Activate(ipAddress, _clock.UtcNow);
            device.SetHardwareId(hardwareId);
            device.SetFirmwareVersion(request.FirmwareVersion);
            device.ActivateMqttCredentials(mqttUsername, _secretHasher.Hash(mqttPassword));
        }
        catch (InvalidOperationException ex)
        {
            return Result<ActivateProvisioningResponse>.Failure(ex.Message, "activation_failed");
        }

        _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
            SecurityAuditAction.ProvisioningActivated,
            true,
            tenantId: device.TenantId,
            details: $"device:{device.Id};provisioning:{provisioning.Id}",
            ipAddress: ipAddress));

        await _db.SaveChangesAsync(cancellationToken);
        _abuseGuard.RegisterSuccess(ipAddress, request.ProvisioningCode, request.HardwareId);

        return Result<ActivateProvisioningResponse>.Success(new ActivateProvisioningResponse(
            device.Id,
            device.TenantId,
            mqttUsername,
            mqttPassword,
            Topic(device.TenantId, device.Id, "command"),
            Topic(device.TenantId, device.Id, "status"),
            Topic(device.TenantId, device.Id, "heartbeat"),
            Topic(device.TenantId, device.Id, "config")));
    }

    public async Task<Result<ProvisioningStatusResponse>> GetStatusAsync(
        Guid provisioningId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var provisioning = await _db.DeviceProvisionings
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == provisioningId, cancellationToken);

        if (provisioning is null)
        {
            return Result<ProvisioningStatusResponse>.Failure("Provisioning não encontrado.", "not_found");
        }

        var manage = await _access.EnsureCanManageDeviceAsync(userId, provisioning.DeviceId, cancellationToken);
        if (!manage.Succeeded)
        {
            return Result<ProvisioningStatusResponse>.Failure(manage.Error!, manage.ErrorCode);
        }

        return Result<ProvisioningStatusResponse>.Success(new ProvisioningStatusResponse(
            provisioning.Id,
            provisioning.DeviceId,
            provisioning.Status,
            provisioning.CreatedAt,
            provisioning.ExpiresAt,
            provisioning.ActivatedAt));
    }

    private static string Topic(Guid tenantId, Guid deviceId, string leaf) =>
        $"domus/{tenantId:D}/{deviceId:D}/{leaf}";
}

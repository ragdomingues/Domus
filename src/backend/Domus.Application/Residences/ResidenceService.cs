using Domus.Application.Abstractions;
using Domus.Application.Common;
using Domus.Application.Security;
using Domus.Domain.Entities;
using Domus.Domain.Enums;
using Domus.Domain.Services;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Domus.Application.Residences;

public interface IResidenceService
{
    Task<Result<ResidenceResponse>> CreateAsync(CreateResidenceRequest request, ResidenceMembershipContext context, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ResidenceResponse>>> ListByTenantAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<Result<ResidenceResponse>> GetAsync(Guid residenceId, Guid userId, CancellationToken cancellationToken = default);
    Task<Result<ResidenceResponse>> UpdateAsync(Guid residenceId, UpdateResidenceRequest request, ResidenceMembershipContext context, CancellationToken cancellationToken = default);
    Task<Result> SoftDeleteAsync(Guid residenceId, ResidenceMembershipContext context, CancellationToken cancellationToken = default);
}

public sealed class CreateResidenceRequestValidator : AbstractValidator<CreateResidenceRequest>
{
    public CreateResidenceRequestValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Timezone)
            .Must(tz => tz is null || TimezoneValidator.IsValidIana(tz))
            .WithMessage("Timezone IANA inválido.");
        RuleFor(x => x.Address).MaximumLength(500);
    }
}

public sealed class UpdateResidenceRequestValidator : AbstractValidator<UpdateResidenceRequest>
{
    public UpdateResidenceRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Timezone).NotEmpty().Must(TimezoneValidator.IsValidIana!);
        RuleFor(x => x.Address).MaximumLength(500);
    }
}

public sealed class ResidenceService : IResidenceService
{
    private readonly IDomusDbContext _db;
    private readonly IAccessControlService _access;
    private readonly IValidator<CreateResidenceRequest> _createValidator;
    private readonly IValidator<UpdateResidenceRequest> _updateValidator;

    public ResidenceService(
        IDomusDbContext db,
        IAccessControlService access,
        IValidator<CreateResidenceRequest> createValidator,
        IValidator<UpdateResidenceRequest> updateValidator)
    {
        _db = db;
        _access = access;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<Result<ResidenceResponse>> CreateAsync(
        CreateResidenceRequest request,
        ResidenceMembershipContext context,
        CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<ResidenceResponse>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), "validation_error");
        }

        var manageTenant = await _access.EnsureCanManageTenantAsync(context.UserId, request.TenantId, cancellationToken);
        if (!manageTenant.Succeeded)
        {
            return Result<ResidenceResponse>.Failure(manageTenant.Error!, manageTenant.ErrorCode);
        }

        var timezone = string.IsNullOrWhiteSpace(request.Timezone)
            ? Residence.DefaultTimezone
            : request.Timezone.Trim();

        var residence = Residence.Create(request.TenantId, request.Name, timezone, request.Address);
        _db.Residences.Add(residence);
        _db.ResidenceMemberships.Add(ResidenceMembership.Create(
            context.UserId,
            residence.Id,
            ResidenceRole.Administrator));

        _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
            SecurityAuditAction.ResidenceCreated,
            true,
            context.UserId,
            request.TenantId,
            context.IpAddress,
            context.UserAgent,
            $"residence:{residence.Id}"));

        await _db.SaveChangesAsync(cancellationToken);
        return Result<ResidenceResponse>.Success(Map(residence));
    }

    public async Task<Result<IReadOnlyList<ResidenceResponse>>> ListByTenantAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var tenantAccess = await _access.EnsureCanAccessTenantAsync(userId, tenantId, cancellationToken);
        if (!tenantAccess.Succeeded)
        {
            return Result<IReadOnlyList<ResidenceResponse>>.Failure(tenantAccess.Error!, tenantAccess.ErrorCode);
        }

        var residenceIds = await _db.ResidenceMemberships
            .Where(m => m.UserId == userId && m.RevokedAt == null)
            .Select(m => m.ResidenceId)
            .ToListAsync(cancellationToken);

        var residences = await _db.Residences
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && residenceIds.Contains(r.Id))
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<ResidenceResponse>>.Success(residences.Select(Map).ToList());
    }

    public async Task<Result<ResidenceResponse>> GetAsync(
        Guid residenceId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var access = await _access.EnsureCanAccessResidenceAsync(userId, residenceId, cancellationToken);
        if (!access.Succeeded)
        {
            return Result<ResidenceResponse>.Failure(access.Error!, access.ErrorCode);
        }

        var residence = await _db.Residences.AsNoTracking().FirstAsync(r => r.Id == residenceId, cancellationToken);
        return Result<ResidenceResponse>.Success(Map(residence));
    }

    public async Task<Result<ResidenceResponse>> UpdateAsync(
        Guid residenceId,
        UpdateResidenceRequest request,
        ResidenceMembershipContext context,
        CancellationToken cancellationToken = default)
    {
        var validation = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<ResidenceResponse>.Failure(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)), "validation_error");
        }

        var manage = await _access.EnsureCanManageResidenceAsync(context.UserId, residenceId, cancellationToken);
        if (!manage.Succeeded)
        {
            return Result<ResidenceResponse>.Failure(manage.Error!, manage.ErrorCode);
        }

        var residence = await _db.Residences.FirstAsync(r => r.Id == residenceId, cancellationToken);
        residence.Rename(request.Name);
        residence.ChangeTimezone(request.Timezone);
        residence.SetAddress(request.Address);

        _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
            SecurityAuditAction.ResidenceUpdated,
            true,
            context.UserId,
            residence.TenantId,
            context.IpAddress,
            context.UserAgent,
            $"residence:{residence.Id}"));

        await _db.SaveChangesAsync(cancellationToken);
        return Result<ResidenceResponse>.Success(Map(residence));
    }

    public async Task<Result> SoftDeleteAsync(
        Guid residenceId,
        ResidenceMembershipContext context,
        CancellationToken cancellationToken = default)
    {
        var manage = await _access.EnsureCanManageResidenceAsync(context.UserId, residenceId, cancellationToken);
        if (!manage.Succeeded)
        {
            return manage;
        }

        var residence = await _db.Residences.FirstAsync(r => r.Id == residenceId, cancellationToken);
        residence.SoftDelete(context.UserId);

        var devices = await _db.Devices
            .Where(d => d.ResidenceId == residenceId)
            .ToListAsync(cancellationToken);
        foreach (var device in devices)
        {
            if (!device.IsDeleted)
            {
                device.SoftDelete(context.UserId);
            }
        }

        var deviceIds = devices.Select(d => d.Id).ToList();
        if (deviceIds.Count > 0)
        {
            var pending = await _db.DeviceProvisionings
                .Where(p => deviceIds.Contains(p.DeviceId) && p.Status == ProvisioningStatus.Pending)
                .ToListAsync(cancellationToken);
            foreach (var item in pending)
            {
                item.Revoke();
            }
        }

        var memberships = await _db.ResidenceMemberships
            .Where(m => m.ResidenceId == residenceId && m.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var membership in memberships)
        {
            membership.Revoke();
        }

        _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
            SecurityAuditAction.ResidenceDeleted,
            true,
            context.UserId,
            residence.TenantId,
            context.IpAddress,
            context.UserAgent,
            $"residence:{residence.Id};devices:{devices.Count};memberships:{memberships.Count}"));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static ResidenceResponse Map(Residence residence) =>
        new(residence.Id, residence.TenantId, residence.Name, residence.Timezone, residence.Address, residence.CreatedAt);
}

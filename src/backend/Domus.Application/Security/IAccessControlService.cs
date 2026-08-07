using Domus.Application.Abstractions;
using Domus.Application.Common;
using Domus.Domain.Entities;
using Domus.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Domus.Application.Security;

public interface IAccessControlService
{
    Task<Result> EnsureCanAccessResidenceAsync(Guid userId, Guid residenceId, CancellationToken cancellationToken = default);
    Task<Result> EnsureCanManageResidenceAsync(Guid userId, Guid residenceId, CancellationToken cancellationToken = default);
    Task<Result> EnsureCanAccessTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result> EnsureCanManageTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result> EnsureCanAccessDeviceAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken = default);
    Task<Result> EnsureCanManageDeviceAsync(Guid userId, Guid deviceId, CancellationToken cancellationToken = default);
}

public sealed class AccessControlService : IAccessControlService
{
    private readonly IDomusDbContext _db;
    private readonly IDateTimeProvider _clock;

    public AccessControlService(IDomusDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Result> EnsureCanAccessResidenceAsync(
        Guid userId,
        Guid residenceId,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadResidenceAccessAsync(userId, residenceId, cancellationToken);
        if (context.Error is not null)
        {
            return context.Error;
        }

        return Result.Success();
    }

    public async Task<Result> EnsureCanManageResidenceAsync(
        Guid userId,
        Guid residenceId,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadResidenceAccessAsync(userId, residenceId, cancellationToken);
        if (context.Error is not null)
        {
            return context.Error;
        }

        if (context.Membership!.Role != ResidenceRole.Administrator)
        {
            await AuditIdorAsync(userId, context.Residence!.TenantId, $"manage_residence:{residenceId}", cancellationToken);
            return Result.Failure("Permissão insuficiente.", "forbidden");
        }

        return Result.Success();
    }

    public async Task<Result> EnsureCanAccessTenantAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var membership = await GetActiveTenantMembershipAsync(userId, tenantId, cancellationToken);
        if (membership is null)
        {
            await AuditIdorAsync(userId, tenantId, $"tenant:{tenantId}", cancellationToken);
            return Result.Failure("Tenant não encontrado ou acesso negado.", "access_denied");
        }

        return Result.Success();
    }

    public async Task<Result> EnsureCanManageTenantAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var membership = await GetActiveTenantMembershipAsync(userId, tenantId, cancellationToken);
        if (membership is null)
        {
            await AuditIdorAsync(userId, tenantId, $"manage_tenant:{tenantId}", cancellationToken);
            return Result.Failure("Tenant não encontrado ou acesso negado.", "access_denied");
        }

        if (membership.Role is not (TenantRole.Owner or TenantRole.Admin))
        {
            await AuditIdorAsync(userId, tenantId, $"manage_tenant_role:{tenantId}", cancellationToken);
            return Result.Failure("Permissão insuficiente.", "forbidden");
        }

        return Result.Success();
    }

    public async Task<Result> EnsureCanAccessDeviceAsync(
        Guid userId,
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var device = await _db.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken);

        if (device is null)
        {
            await AuditIdorAsync(userId, null, $"device:{deviceId}", cancellationToken);
            return Result.Failure("Dispositivo não encontrado ou acesso negado.", "access_denied");
        }

        var residenceAccess = await EnsureCanAccessResidenceAsync(userId, device.ResidenceId, cancellationToken);
        if (!residenceAccess.Succeeded)
        {
            return residenceAccess;
        }

        var residence = await _db.Residences
            .AsNoTracking()
            .FirstAsync(r => r.Id == device.ResidenceId, cancellationToken);

        if (residence.TenantId != device.TenantId)
        {
            await AuditIdorAsync(userId, device.TenantId, $"device_tenant_mismatch:{deviceId}", cancellationToken);
            return Result.Failure("Dispositivo não encontrado ou acesso negado.", "access_denied");
        }

        return Result.Success();
    }

    public async Task<Result> EnsureCanManageDeviceAsync(
        Guid userId,
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var device = await _db.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken);

        if (device is null)
        {
            await AuditIdorAsync(userId, null, $"manage_device:{deviceId}", cancellationToken);
            return Result.Failure("Dispositivo não encontrado ou acesso negado.", "access_denied");
        }

        return await EnsureCanManageResidenceAsync(userId, device.ResidenceId, cancellationToken);
    }

    private async Task<(Residence? Residence, ResidenceMembership? Membership, Result? Error)> LoadResidenceAccessAsync(
        Guid userId,
        Guid residenceId,
        CancellationToken cancellationToken)
    {
        var residence = await _db.Residences
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == residenceId, cancellationToken);

        if (residence is null)
        {
            await AuditIdorAsync(userId, null, $"residence:{residenceId}", cancellationToken);
            return (null, null, Result.Failure("Residência não encontrada ou acesso negado.", "access_denied"));
        }

        var membership = await _db.ResidenceMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.UserId == userId && m.ResidenceId == residenceId,
                cancellationToken);

        if (membership is null || !membership.IsActiveAt(_clock.UtcNow))
        {
            await AuditIdorAsync(userId, residence.TenantId, $"residence:{residenceId}", cancellationToken);
            return (residence, null, Result.Failure("Residência não encontrada ou acesso negado.", "access_denied"));
        }

        var tenantActive = await _db.Tenants
            .AsNoTracking()
            .AnyAsync(t => t.Id == residence.TenantId && t.Status == TenantStatus.Active, cancellationToken);

        if (!tenantActive)
        {
            return (residence, membership, Result.Failure("Tenant inativo.", "tenant_inactive"));
        }

        return (residence, membership, null);
    }

    private async Task<TenantMembership?> GetActiveTenantMembershipAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var membership = await _db.TenantMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.UserId == userId && m.TenantId == tenantId && m.RevokedAt == null,
                cancellationToken);

        if (membership is null)
        {
            return null;
        }

        var tenantActive = await _db.Tenants
            .AsNoTracking()
            .AnyAsync(t => t.Id == tenantId && t.Status == TenantStatus.Active, cancellationToken);

        return tenantActive ? membership : null;
    }

    private async Task AuditIdorAsync(
        Guid userId,
        Guid? tenantId,
        string details,
        CancellationToken cancellationToken)
    {
        _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
            SecurityAuditAction.IdorBlocked,
            succeeded: false,
            userId: userId,
            tenantId: tenantId,
            details: details));
        await _db.SaveChangesAsync(cancellationToken);
    }
}

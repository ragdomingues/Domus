using Domus.Application.Abstractions;
using Domus.Application.Common;
using Domus.Application.Security;
using Domus.Domain.Entities;
using Domus.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Domus.Application.Residences;

public sealed record ResidenceMemberResponse(
    Guid MembershipId,
    Guid UserId,
    string Email,
    string Name,
    ResidenceRole Role,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidUntil,
    bool IsActive);

public sealed record InviteMemberRequest(
    Guid ResidenceId,
    string Email,
    string? Name,
    ResidenceRole Role,
    int? ValidUntilDays = null);

public sealed record InviteMemberResponse(
    Guid MembershipId,
    Guid UserId,
    string Email,
    string Name,
    ResidenceRole Role,
    bool CreatedNewUser,
    string? TemporaryPassword);

public sealed record UpdateMemberRoleRequest(ResidenceRole Role, int? ValidUntilDays = null);

public interface IMembershipService
{
    Task<Result<IReadOnlyList<ResidenceMemberResponse>>> ListAsync(
        Guid residenceId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<Result<InviteMemberResponse>> InviteAsync(
        InviteMemberRequest request,
        ResidenceMembershipContext context,
        CancellationToken cancellationToken = default);

    Task<Result<ResidenceMemberResponse>> UpdateRoleAsync(
        Guid residenceId,
        Guid membershipId,
        UpdateMemberRoleRequest request,
        ResidenceMembershipContext context,
        CancellationToken cancellationToken = default);

    Task<Result> RevokeAsync(
        Guid residenceId,
        Guid membershipId,
        ResidenceMembershipContext context,
        CancellationToken cancellationToken = default);
}

public sealed class InviteMemberRequestValidator : AbstractValidator<InviteMemberRequest>
{
    public InviteMemberRequestValidator()
    {
        RuleFor(x => x.ResidenceId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Name).MaximumLength(200);
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.ValidUntilDays).InclusiveBetween(1, 365).When(x => x.ValidUntilDays.HasValue);
    }
}

public sealed class MembershipService : IMembershipService
{
    private readonly IDomusDbContext _db;
    private readonly IAccessControlService _access;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISecureTokenGenerator _tokens;
    private readonly IDateTimeProvider _clock;
    private readonly IValidator<InviteMemberRequest> _inviteValidator;

    public MembershipService(
        IDomusDbContext db,
        IAccessControlService access,
        IPasswordHasher passwordHasher,
        ISecureTokenGenerator tokens,
        IDateTimeProvider clock,
        IValidator<InviteMemberRequest> inviteValidator)
    {
        _db = db;
        _access = access;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _clock = clock;
        _inviteValidator = inviteValidator;
    }

    public async Task<Result<IReadOnlyList<ResidenceMemberResponse>>> ListAsync(
        Guid residenceId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var access = await _access.EnsureCanAccessResidenceAsync(actorUserId, residenceId, cancellationToken);
        if (!access.Succeeded)
        {
            return Result<IReadOnlyList<ResidenceMemberResponse>>.Failure(access.Error!, access.ErrorCode);
        }

        var now = _clock.UtcNow;
        var items = await _db.ResidenceMemberships.AsNoTracking()
            .Where(m => m.ResidenceId == residenceId)
            .Join(
                _db.Users.AsNoTracking(),
                m => m.UserId,
                u => u.Id,
                (m, u) => new { m, u })
            .OrderBy(x => x.u.Name)
            .ToListAsync(cancellationToken);

        var response = items
            .Select(x => new ResidenceMemberResponse(
                x.m.Id,
                x.u.Id,
                x.u.Email,
                x.u.Name,
                x.m.Role,
                x.m.ValidFrom,
                x.m.ValidUntil,
                x.m.IsActiveAt(now)))
            .ToList();

        return Result<IReadOnlyList<ResidenceMemberResponse>>.Success(response);
    }

    public async Task<Result<InviteMemberResponse>> InviteAsync(
        InviteMemberRequest request,
        ResidenceMembershipContext context,
        CancellationToken cancellationToken = default)
    {
        var validation = await _inviteValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<InviteMemberResponse>.Failure(
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)),
                "validation_error");
        }

        var manage = await _access.EnsureCanManageResidenceAsync(context.UserId, request.ResidenceId, cancellationToken);
        if (!manage.Succeeded)
        {
            return Result<InviteMemberResponse>.Failure(manage.Error!, manage.ErrorCode);
        }

        var residence = await _db.Residences.FirstAsync(r => r.Id == request.ResidenceId, cancellationToken);
        var email = request.Email.Trim().ToLowerInvariant();
        var validUntil = request.Role == ResidenceRole.Visitor && request.ValidUntilDays is int days
            ? _clock.UtcNow.AddDays(days)
            : (DateTimeOffset?)null;

        if (request.Role == ResidenceRole.Visitor && validUntil is null)
        {
            validUntil = _clock.UtcNow.AddDays(7);
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        string? temporaryPassword = null;
        var createdNewUser = false;

        if (user is null)
        {
            temporaryPassword = _tokens.GenerateTemporaryPassword();
            var name = string.IsNullOrWhiteSpace(request.Name)
                ? email.Split('@')[0]
                : request.Name.Trim();

            user = User.Create(email, _passwordHasher.Hash(temporaryPassword), name);
            _db.Users.Add(user);
            createdNewUser = true;
        }

        var tenantMembership = await _db.TenantMemberships
            .FirstOrDefaultAsync(m => m.UserId == user.Id && m.TenantId == residence.TenantId, cancellationToken);

        if (tenantMembership is null)
        {
            _db.TenantMemberships.Add(TenantMembership.Create(user.Id, residence.TenantId, TenantRole.Member));
        }
        else if (tenantMembership.RevokedAt is not null)
        {
            tenantMembership.Reactivate(TenantRole.Member);
        }

        var membership = await _db.ResidenceMemberships
            .FirstOrDefaultAsync(m => m.UserId == user.Id && m.ResidenceId == residence.Id, cancellationToken);

        if (membership is null)
        {
            membership = ResidenceMembership.Create(user.Id, residence.Id, request.Role, validUntil: validUntil);
            _db.ResidenceMemberships.Add(membership);
        }
        else if (membership.IsActiveAt(_clock.UtcNow))
        {
            return Result<InviteMemberResponse>.Failure("Usuário já é membro desta residência.", "already_member");
        }
        else
        {
            membership.Reactivate(request.Role, validUntil);
        }

        _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
            SecurityAuditAction.MemberInvited,
            true,
            context.UserId,
            residence.TenantId,
            context.IpAddress,
            context.UserAgent,
            $"residence:{residence.Id};user:{user.Id};role:{request.Role};newUser:{createdNewUser}"));

        await _db.SaveChangesAsync(cancellationToken);

        return Result<InviteMemberResponse>.Success(new InviteMemberResponse(
            membership.Id,
            user.Id,
            user.Email,
            user.Name,
            request.Role,
            createdNewUser,
            temporaryPassword));
    }

    public async Task<Result<ResidenceMemberResponse>> UpdateRoleAsync(
        Guid residenceId,
        Guid membershipId,
        UpdateMemberRoleRequest request,
        ResidenceMembershipContext context,
        CancellationToken cancellationToken = default)
    {
        var manage = await _access.EnsureCanManageResidenceAsync(context.UserId, residenceId, cancellationToken);
        if (!manage.Succeeded)
        {
            return Result<ResidenceMemberResponse>.Failure(manage.Error!, manage.ErrorCode);
        }

        var membership = await _db.ResidenceMemberships
            .FirstOrDefaultAsync(m => m.Id == membershipId && m.ResidenceId == residenceId, cancellationToken);
        if (membership is null)
        {
            return Result<ResidenceMemberResponse>.Failure("Membro não encontrado.", "not_found");
        }

        if (membership.UserId == context.UserId)
        {
            return Result<ResidenceMemberResponse>.Failure("Não é possível alterar o próprio papel por este endpoint.", "invalid_operation");
        }

        var validUntil = request.Role == ResidenceRole.Visitor && request.ValidUntilDays is int days
            ? _clock.UtcNow.AddDays(days)
            : (DateTimeOffset?)null;

        membership.UpdateRole(request.Role, validUntil);
        await _db.SaveChangesAsync(cancellationToken);

        var user = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == membership.UserId, cancellationToken);
        return Result<ResidenceMemberResponse>.Success(new ResidenceMemberResponse(
            membership.Id,
            user.Id,
            user.Email,
            user.Name,
            membership.Role,
            membership.ValidFrom,
            membership.ValidUntil,
            membership.IsActiveAt(_clock.UtcNow)));
    }

    public async Task<Result> RevokeAsync(
        Guid residenceId,
        Guid membershipId,
        ResidenceMembershipContext context,
        CancellationToken cancellationToken = default)
    {
        var manage = await _access.EnsureCanManageResidenceAsync(context.UserId, residenceId, cancellationToken);
        if (!manage.Succeeded)
        {
            return Result.Failure(manage.Error!, manage.ErrorCode);
        }

        var membership = await _db.ResidenceMemberships
            .FirstOrDefaultAsync(m => m.Id == membershipId && m.ResidenceId == residenceId, cancellationToken);
        if (membership is null)
        {
            return Result.Failure("Membro não encontrado.", "not_found");
        }

        if (membership.UserId == context.UserId)
        {
            return Result.Failure("Não é possível remover a si mesmo.", "invalid_operation");
        }

        membership.Revoke(_clock.UtcNow);
        var residence = await _db.Residences.AsNoTracking().FirstAsync(r => r.Id == residenceId, cancellationToken);
        _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
            SecurityAuditAction.MemberRevoked,
            true,
            context.UserId,
            residence.TenantId,
            context.IpAddress,
            context.UserAgent,
            $"residence:{residenceId};membership:{membershipId}"));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

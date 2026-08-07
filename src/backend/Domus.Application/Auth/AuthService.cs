using Domus.Application.Abstractions;
using Domus.Application.Common;
using Domus.Domain.Entities;
using Domus.Domain.Enums;
using Domus.Domain.Services;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Domus.Application.Auth;

public interface IAuthService
{
    Task<Result<AuthTokensResponse>> RegisterAsync(RegisterRequest request, AuthContextInfo context, CancellationToken cancellationToken = default);
    Task<Result<AuthTokensResponse>> LoginAsync(LoginRequest request, AuthContextInfo context, CancellationToken cancellationToken = default);
    Task<Result<AuthTokensResponse>> RefreshAsync(RefreshRequest request, AuthContextInfo context, CancellationToken cancellationToken = default);
    Task<Result> LogoutAsync(LogoutRequest request, AuthContextInfo context, CancellationToken cancellationToken = default);
    Task<Result<ForgotPasswordResponse>> ForgotPasswordAsync(ForgotPasswordRequest request, AuthContextInfo context, CancellationToken cancellationToken = default);
    Task<Result> ResetPasswordAsync(ResetPasswordRequest request, AuthContextInfo context, CancellationToken cancellationToken = default);
    Task<Result<UserProfileResponse>> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<UserProfileResponse>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, AuthContextInfo context, CancellationToken cancellationToken = default);
    Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, AuthContextInfo context, CancellationToken cancellationToken = default);
}

public sealed class AuthService : IAuthService
{
    private const string ForgotPasswordMessage =
        "Se o e-mail estiver cadastrado, enviaremos instruções para redefinir a senha.";

    private readonly IDomusDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IDateTimeProvider _clock;
    private readonly ISecureTokenGenerator _secureTokens;
    private readonly ISecretHasher _secretHasher;
    private readonly IEmailSender _emailSender;
    private readonly AuthOptions _authOptions;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<RefreshRequest> _refreshValidator;
    private readonly IValidator<ForgotPasswordRequest> _forgotValidator;
    private readonly IValidator<ResetPasswordRequest> _resetValidator;
    private readonly IValidator<UpdateProfileRequest> _profileValidator;
    private readonly IValidator<ChangePasswordRequest> _changePasswordValidator;

    public AuthService(
        IDomusDbContext db,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IDateTimeProvider clock,
        ISecureTokenGenerator secureTokens,
        ISecretHasher secretHasher,
        IEmailSender emailSender,
        IOptions<AuthOptions> authOptions,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<RefreshRequest> refreshValidator,
        IValidator<ForgotPasswordRequest> forgotValidator,
        IValidator<ResetPasswordRequest> resetValidator,
        IValidator<UpdateProfileRequest> profileValidator,
        IValidator<ChangePasswordRequest> changePasswordValidator)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _clock = clock;
        _secureTokens = secureTokens;
        _secretHasher = secretHasher;
        _emailSender = emailSender;
        _authOptions = authOptions.Value;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _refreshValidator = refreshValidator;
        _forgotValidator = forgotValidator;
        _resetValidator = resetValidator;
        _profileValidator = profileValidator;
        _changePasswordValidator = changePasswordValidator;
    }

    public async Task<Result<AuthTokensResponse>> RegisterAsync(
        RegisterRequest request,
        AuthContextInfo context,
        CancellationToken cancellationToken = default)
    {
        var validation = await _registerValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<AuthTokensResponse>.Failure(
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)),
                "validation_error");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var exists = await _db.Users.AnyAsync(u => u.Email == email, cancellationToken);
        if (exists)
        {
            return Result<AuthTokensResponse>.Failure("E-mail já cadastrado.", "email_exists");
        }

        var timezone = string.IsNullOrWhiteSpace(request.Timezone)
            ? ResidenceDefaults.Timezone
            : request.Timezone.Trim();

        if (!TimezoneValidator.IsValidIana(timezone))
        {
            return Result<AuthTokensResponse>.Failure("Timezone IANA inválido.", "invalid_timezone");
        }

        var user = User.Create(email, _passwordHasher.Hash(request.Password), request.Name);
        var tenant = Tenant.Create(request.TenantName, SlugGenerator.UniqueFromName(request.TenantName));
        var residenceName = string.IsNullOrWhiteSpace(request.ResidenceName)
            ? "Casa Principal"
            : request.ResidenceName!;
        var residence = Residence.Create(tenant.Id, residenceName, timezone);

        var tenantMembership = TenantMembership.Create(user.Id, tenant.Id, TenantRole.Owner);
        var residenceMembership = ResidenceMembership.Create(
            user.Id,
            residence.Id,
            ResidenceRole.Administrator);

        _db.Users.Add(user);
        _db.Tenants.Add(tenant);
        _db.Residences.Add(residence);
        _db.TenantMemberships.Add(tenantMembership);
        _db.ResidenceMemberships.Add(residenceMembership);

        var tokens = await IssueTokensAsync(user, tenant.Id, residence.Id, context, cancellationToken);

        _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
            SecurityAuditAction.RegisterSucceeded,
            succeeded: true,
            userId: user.Id,
            tenantId: tenant.Id,
            ipAddress: context.IpAddress,
            userAgent: context.UserAgent));

        await _db.SaveChangesAsync(cancellationToken);
        return Result<AuthTokensResponse>.Success(tokens);
    }

    public async Task<Result<AuthTokensResponse>> LoginAsync(
        LoginRequest request,
        AuthContextInfo context,
        CancellationToken cancellationToken = default)
    {
        var validation = await _loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<AuthTokensResponse>.Failure("Credenciais inválidas.", "invalid_credentials");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null || user.Status != UserStatus.Active || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
                SecurityAuditAction.LoginFailed,
                succeeded: false,
                userId: user?.Id,
                ipAddress: context.IpAddress,
                userAgent: context.UserAgent,
                details: MaskEmail(email)));
            await _db.SaveChangesAsync(cancellationToken);
            return Result<AuthTokensResponse>.Failure("Credenciais inválidas.", "invalid_credentials");
        }

        var tenantId = await ResolveActiveTenantIdAsync(user.Id, cancellationToken);
        if (tenantId is null)
        {
            return Result<AuthTokensResponse>.Failure("Usuário sem tenant ativo.", "no_tenant");
        }

        var residenceId = await ResolveActiveResidenceIdAsync(user.Id, cancellationToken);

        var loginContext = context with { DeviceInfo = request.DeviceInfo ?? context.DeviceInfo };
        var tokens = await IssueTokensAsync(user, tenantId.Value, residenceId, loginContext, cancellationToken);

        _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
            SecurityAuditAction.LoginSucceeded,
            succeeded: true,
            userId: user.Id,
            tenantId: tenantId.Value,
            ipAddress: context.IpAddress,
            userAgent: context.UserAgent));

        await _db.SaveChangesAsync(cancellationToken);
        return Result<AuthTokensResponse>.Success(tokens);
    }

    public async Task<Result<AuthTokensResponse>> RefreshAsync(
        RefreshRequest request,
        AuthContextInfo context,
        CancellationToken cancellationToken = default)
    {
        var validation = await _refreshValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<AuthTokensResponse>.Failure("Refresh token inválido.", "invalid_refresh");
        }

        var hash = _tokenService.HashToken(request.RefreshToken);
        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (existing is null)
        {
            return Result<AuthTokensResponse>.Failure("Refresh token inválido.", "invalid_refresh");
        }

        // Expired (not revoked): reject without killing the family.
        if (existing.RevokedAt is null && existing.ExpiresAt < _clock.UtcNow)
        {
            return Result<AuthTokensResponse>.Failure("Refresh token expirado.", "refresh_expired");
        }

        // Revoked token reuse: revoke entire family (theft detection).
        if (existing.RevokedAt is not null)
        {
            await RevokeFamilyAsync(existing.FamilyId, cancellationToken);
            _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
                SecurityAuditAction.RefreshReuseDetected,
                succeeded: false,
                userId: existing.UserId,
                ipAddress: context.IpAddress,
                userAgent: context.UserAgent,
                details: "Attempt to reuse revoked refresh token"));
            await _db.SaveChangesAsync(cancellationToken);
            return Result<AuthTokensResponse>.Failure("Refresh token revogado.", "refresh_reuse");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == existing.UserId, cancellationToken);
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result<AuthTokensResponse>.Failure("Usuário inválido.", "invalid_user");
        }

        var tenantId = await ResolveActiveTenantIdAsync(user.Id, cancellationToken) ?? Guid.Empty;
        var residenceId = await ResolveActiveResidenceIdAsync(user.Id, cancellationToken);

        var (plain, newHash, expiresAt) = _tokenService.CreateRefreshToken();
        var replacement = RefreshToken.Create(
            user.Id,
            newHash,
            existing.FamilyId,
            expiresAt,
            context.DeviceInfo,
            context.IpAddress);

        existing.Revoke(replacement.Id, _clock.UtcNow);
        _db.RefreshTokens.Add(replacement);

        var tenantIds = await _db.TenantMemberships
            .Where(m => m.UserId == user.Id && m.RevokedAt == null)
            .Select(m => m.TenantId)
            .ToListAsync(cancellationToken);

        var accessToken = _tokenService.CreateAccessToken(user, tenantIds);
        var accessExpires = _clock.UtcNow.Add(_tokenService.AccessTokenLifetime);

        _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
            SecurityAuditAction.RefreshSucceeded,
            succeeded: true,
            userId: user.Id,
            tenantId: tenantId == Guid.Empty ? null : tenantId,
            ipAddress: context.IpAddress,
            userAgent: context.UserAgent));

        await _db.SaveChangesAsync(cancellationToken);

        return Result<AuthTokensResponse>.Success(new AuthTokensResponse(
            accessToken,
            plain,
            accessExpires,
            expiresAt,
            user.Id,
            user.Email,
            user.Name,
            tenantId,
            residenceId));
    }

    public async Task<Result> LogoutAsync(
        LogoutRequest request,
        AuthContextInfo context,
        CancellationToken cancellationToken = default)
    {
        var hash = _tokenService.HashToken(request.RefreshToken);
        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (existing is null)
        {
            return Result.Success();
        }

        await RevokeFamilyAsync(existing.FamilyId, cancellationToken);
        _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
            SecurityAuditAction.Logout,
            succeeded: true,
            userId: existing.UserId,
            ipAddress: context.IpAddress,
            userAgent: context.UserAgent));
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<ForgotPasswordResponse>> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        AuthContextInfo context,
        CancellationToken cancellationToken = default)
    {
        var validation = await _forgotValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<ForgotPasswordResponse>.Failure(
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)),
                "validation_error");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.Email == email && u.Status == UserStatus.Active,
            cancellationToken);

        // Resposta genérica evita enumeração de e-mails.
        if (user is null)
        {
            _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
                SecurityAuditAction.PasswordResetRequested,
                succeeded: true,
                ipAddress: context.IpAddress,
                userAgent: context.UserAgent,
                details: $"unknown:{MaskEmail(email)}"));
            await _db.SaveChangesAsync(cancellationToken);
            return Result<ForgotPasswordResponse>.Success(new ForgotPasswordResponse(ForgotPasswordMessage));
        }

        var pending = await _db.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var old in pending)
        {
            old.MarkUsed(_clock.UtcNow);
        }

        var minutes = Math.Clamp(_authOptions.PasswordResetTokenMinutes, 5, 120);
        var plainToken = _secureTokens.GeneratePasswordResetToken();
        var expiresAt = _clock.UtcNow.AddMinutes(minutes);
        _db.PasswordResetTokens.Add(PasswordResetToken.Create(
            user.Id,
            _secretHasher.Hash(plainToken),
            expiresAt));

        await _emailSender.SendPasswordResetAsync(
            user.Email,
            user.Name,
            plainToken,
            TimeSpan.FromMinutes(minutes),
            cancellationToken);

        _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
            SecurityAuditAction.PasswordResetRequested,
            succeeded: true,
            userId: user.Id,
            ipAddress: context.IpAddress,
            userAgent: context.UserAgent,
            details: $"email:{MaskEmail(email)};expires:{expiresAt:O}"));

        await _db.SaveChangesAsync(cancellationToken);

        return Result<ForgotPasswordResponse>.Success(new ForgotPasswordResponse(
            ForgotPasswordMessage,
            _authOptions.ExposeResetToken ? plainToken : null,
            _authOptions.ExposeResetToken ? expiresAt : null));
    }

    public async Task<Result> ResetPasswordAsync(
        ResetPasswordRequest request,
        AuthContextInfo context,
        CancellationToken cancellationToken = default)
    {
        var validation = await _resetValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)),
                "validation_error");
        }

        var tokenHash = _secretHasher.Hash(request.Token.Trim());
        var reset = await _db.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (reset is null || !reset.IsUsable(_clock.UtcNow))
        {
            _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
                SecurityAuditAction.PasswordResetFailed,
                succeeded: false,
                ipAddress: context.IpAddress,
                userAgent: context.UserAgent,
                details: "invalid_or_expired_token"));
            await _db.SaveChangesAsync(cancellationToken);
            return Result.Failure("Token inválido ou expirado.", "invalid_reset_token");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == reset.UserId, cancellationToken);
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure("Usuário inválido.", "invalid_user");
        }

        user.UpdatePasswordHash(_passwordHasher.Hash(request.NewPassword));
        reset.MarkUsed(_clock.UtcNow);

        var activeRefresh = await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in activeRefresh)
        {
            token.Revoke(at: _clock.UtcNow);
        }

        _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
            SecurityAuditAction.PasswordResetSucceeded,
            succeeded: true,
            userId: user.Id,
            ipAddress: context.IpAddress,
            userAgent: context.UserAgent));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<UserProfileResponse>> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.Id == userId && u.Status == UserStatus.Active,
            cancellationToken);
        if (user is null)
        {
            return Result<UserProfileResponse>.Failure("Usuário não encontrado.", "not_found");
        }

        var tenantId = await ResolveActiveTenantIdAsync(userId, cancellationToken);
        var residenceId = await ResolveActiveResidenceIdAsync(userId, cancellationToken);
        return Result<UserProfileResponse>.Success(
            new UserProfileResponse(user.Id, user.Email, user.Name, tenantId, residenceId));
    }

    public async Task<Result<UserProfileResponse>> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        AuthContextInfo context,
        CancellationToken cancellationToken = default)
    {
        var validation = await _profileValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<UserProfileResponse>.Failure(
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)),
                "validation_error");
        }

        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.Id == userId && u.Status == UserStatus.Active,
            cancellationToken);
        if (user is null)
        {
            return Result<UserProfileResponse>.Failure("Usuário não encontrado.", "not_found");
        }

        user.Rename(request.Name);
        await _db.SaveChangesAsync(cancellationToken);

        var tenantId = await ResolveActiveTenantIdAsync(userId, cancellationToken);
        var residenceId = await ResolveActiveResidenceIdAsync(userId, cancellationToken);
        return Result<UserProfileResponse>.Success(
            new UserProfileResponse(user.Id, user.Email, user.Name, tenantId, residenceId));
    }

    public async Task<Result> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        AuthContextInfo context,
        CancellationToken cancellationToken = default)
    {
        var validation = await _changePasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(
                string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)),
                "validation_error");
        }

        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.Id == userId && u.Status == UserStatus.Active,
            cancellationToken);
        if (user is null)
        {
            return Result.Failure("Usuário não encontrado.", "not_found");
        }

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return Result.Failure("Senha atual incorreta.", "invalid_credentials");
        }

        user.UpdatePasswordHash(_passwordHasher.Hash(request.NewPassword));

        var activeRefresh = await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in activeRefresh)
        {
            token.Revoke(at: _clock.UtcNow);
        }

        _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
            SecurityAuditAction.PasswordResetSucceeded,
            succeeded: true,
            userId: user.Id,
            ipAddress: context.IpAddress,
            userAgent: context.UserAgent,
            details: "change_password"));

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<AuthTokensResponse> IssueTokensAsync(
        User user,
        Guid tenantId,
        Guid? residenceId,
        AuthContextInfo context,
        CancellationToken cancellationToken)
    {
        var tenantIds = await _db.TenantMemberships
            .Where(m => m.UserId == user.Id && m.RevokedAt == null)
            .Select(m => m.TenantId)
            .ToListAsync(cancellationToken);

        if (tenantIds.Count == 0 && tenantId != Guid.Empty)
        {
            tenantIds.Add(tenantId);
        }

        var accessToken = _tokenService.CreateAccessToken(user, tenantIds);
        var (plain, hash, refreshExpires) = _tokenService.CreateRefreshToken();
        var familyId = Guid.NewGuid();
        var refresh = RefreshToken.Create(
            user.Id,
            hash,
            familyId,
            refreshExpires,
            context.DeviceInfo,
            context.IpAddress);
        _db.RefreshTokens.Add(refresh);

        return new AuthTokensResponse(
            accessToken,
            plain,
            _clock.UtcNow.Add(_tokenService.AccessTokenLifetime),
            refreshExpires,
            user.Id,
            user.Email,
            user.Name,
            tenantId,
            residenceId);
    }

    private async Task RevokeFamilyAsync(Guid familyId, CancellationToken cancellationToken)
    {
        var tokens = await _db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.Revoke(at: _clock.UtcNow);
        }

        if (tokens.Count > 0)
        {
            _db.SecurityAuditLogs.Add(SecurityAuditLog.Create(
                SecurityAuditAction.RefreshRevoked,
                succeeded: true,
                userId: tokens[0].UserId,
                details: $"family:{familyId}"));
        }
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1)
        {
            return "***";
        }

        return $"{email[0]}***{email[at..]}";
    }

    private async Task<Guid?> ResolveActiveTenantIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _db.TenantMemberships
            .Where(m => m.UserId == userId && m.RevokedAt == null)
            .Join(
                _db.Tenants.Where(t => t.Status == TenantStatus.Active),
                m => m.TenantId,
                t => t.Id,
                (m, _) => (Guid?)m.TenantId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Guid?> ResolveActiveResidenceIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var memberships = await _db.ResidenceMemberships
            .Where(m => m.UserId == userId && m.RevokedAt == null)
            .ToListAsync(cancellationToken);

        return memberships
            .Where(m => m.IsActiveAt(now))
            .Select(m => (Guid?)m.ResidenceId)
            .FirstOrDefault();
    }
}

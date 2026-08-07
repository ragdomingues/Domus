namespace Domus.Application.Auth;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string Name,
    string TenantName,
    string? ResidenceName = null,
    string? Timezone = null);

public sealed record LoginRequest(
    string Email,
    string Password,
    string? DeviceInfo = null);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ForgotPasswordResponse(
    string Message,
    string? ResetToken = null,
    DateTimeOffset? ExpiresAt = null);

public sealed record ResetPasswordRequest(string Token, string NewPassword);

public sealed record UpdateProfileRequest(string Name);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record UserProfileResponse(
    Guid UserId,
    string Email,
    string Name,
    Guid? TenantId,
    Guid? ResidenceId);

public sealed record AuthTokensResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    Guid UserId,
    string Email,
    string Name,
    Guid TenantId,
    Guid? ResidenceId);

public sealed record AuthContextInfo(
    string? IpAddress,
    string? UserAgent,
    string? DeviceInfo);

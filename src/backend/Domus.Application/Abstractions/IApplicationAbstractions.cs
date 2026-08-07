using Domus.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Domus.Application.Abstractions;

public interface IDomusDbContext
{
    DbSet<User> Users { get; }
    DbSet<Tenant> Tenants { get; }
    DbSet<TenantMembership> TenantMemberships { get; }
    DbSet<Residence> Residences { get; }
    DbSet<ResidenceMembership> ResidenceMemberships { get; }
    DbSet<Device> Devices { get; }
    DbSet<Gate> Gates { get; }
    DbSet<DeviceConfiguration> DeviceConfigurations { get; }
    DbSet<DeviceProvisioning> DeviceProvisionings { get; }
    DbSet<Command> Commands { get; }
    DbSet<DeviceEvent> DeviceEvents { get; }
    DbSet<SecurityAuditLog> SecurityAuditLogs { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }
    DbSet<DevicePermission> DevicePermissions { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<NotificationDelivery> NotificationDeliveries { get; }
    DbSet<UserDeviceNotificationPreference> UserDeviceNotificationPreferences { get; }
    DbSet<UserPushToken> UserPushTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}

public interface ITokenService
{
    TimeSpan AccessTokenLifetime { get; }
    string CreateAccessToken(User user, IEnumerable<Guid> tenantIds);
    (string PlainToken, string TokenHash, DateTimeOffset ExpiresAt) CreateRefreshToken();
    string HashToken(string plainToken);
}

public interface IDeviceMessenger
{
    Task PublishCommandAsync(
        Guid tenantId,
        Guid deviceId,
        Guid commandId,
        Guid correlationId,
        string action,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

    Task PublishConfigurationAsync(
        Guid tenantId,
        Guid deviceId,
        string configurationJson,
        CancellationToken cancellationToken = default,
        bool retain = true);
}

public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}

public interface ISecretHasher
{
    string Hash(string value);
    bool Verify(string value, string hash);
}

public interface ISecureTokenGenerator
{
    string GenerateProvisioningCode();
    string GenerateMqttSecret();
    string GenerateTemporaryPassword();
    string GeneratePasswordResetToken();
    string GenerateMqttUsername(Guid deviceId);
}

public interface IEmailSender
{
    Task SendPasswordResetAsync(
        string email,
        string recipientName,
        string resetToken,
        TimeSpan validFor,
        CancellationToken cancellationToken = default);
}

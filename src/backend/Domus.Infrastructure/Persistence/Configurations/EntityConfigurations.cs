using Domus.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Domus.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        // Soft-delete friendly: allow reuse of email after soft delete.
        builder.HasIndex(x => x.Email)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");
        builder.HasIndex(x => x.CreatedAt);
        builder.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
    }
}

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Slug)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");
        builder.HasIndex(x => x.CreatedAt);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
    }
}

public sealed class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.ToTable("tenant_memberships");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.UserId, x.TenantId }).IsUnique();
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.CreatedAt);
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
        builder.HasOne(x => x.User).WithMany(x => x.TenantMemberships).HasForeignKey(x => x.UserId);
        builder.HasOne(x => x.Tenant).WithMany(x => x.Memberships).HasForeignKey(x => x.TenantId);
    }
}

public sealed class ResidenceConfiguration : IEntityTypeConfiguration<Residence>
{
    public void Configure(EntityTypeBuilder<Residence> builder)
    {
        builder.ToTable("residences");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Timezone).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });
        builder.HasIndex(x => x.CreatedAt);
        builder.HasOne(x => x.Tenant).WithMany(x => x.Residences).HasForeignKey(x => x.TenantId);
    }
}

public sealed class ResidenceMembershipConfiguration : IEntityTypeConfiguration<ResidenceMembership>
{
    public void Configure(EntityTypeBuilder<ResidenceMembership> builder)
    {
        builder.ToTable("residence_memberships");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.UserId, x.ResidenceId }).IsUnique();
        builder.HasIndex(x => x.ResidenceId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.CreatedAt);
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
        builder.HasOne(x => x.User).WithMany(x => x.ResidenceMemberships).HasForeignKey(x => x.UserId);
        builder.HasOne(x => x.Residence).WithMany(x => x.Memberships).HasForeignKey(x => x.ResidenceId);
    }
}

public sealed class DeviceConfigurationEntityConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.LifecycleStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ConnectionStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.FirmwareVersion).HasMaxLength(64);
        builder.Property(x => x.MqttUsername).HasMaxLength(128);
        builder.Property(x => x.MqttSecretHash).HasMaxLength(512);
        builder.Property(x => x.HardwareId).HasMaxLength(128);
        builder.Property(x => x.IsSimulated).HasDefaultValue(false);
        builder.HasIndex(x => x.MqttUsername).IsUnique().HasFilter("\"MqttUsername\" IS NOT NULL");
        builder.HasIndex(x => x.HardwareId).IsUnique().HasFilter("\"HardwareId\" IS NOT NULL");
        builder.HasIndex(x => x.LifecycleStatus);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.ResidenceId);
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });
        builder.HasIndex(x => x.CreatedAt);
        builder.HasOne(x => x.Residence).WithMany(x => x.Devices).HasForeignKey(x => x.ResidenceId);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GateConfiguration : IEntityTypeConfiguration<Gate>
{
    public void Configure(EntityTypeBuilder<Gate> builder)
    {
        builder.ToTable("gates");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.DeviceId).IsUnique();
        builder.HasIndex(x => new { x.GateState, x.OpenedAt });
        builder.Property(x => x.GateState).HasConversion<string>().HasMaxLength(32);
        builder.HasOne(x => x.Device).WithOne(x => x.Gate).HasForeignKey<Gate>(x => x.DeviceId);
    }
}

public sealed class DeviceConfigurationConfiguration : IEntityTypeConfiguration<DeviceConfiguration>
{
    public void Configure(EntityTypeBuilder<DeviceConfiguration> builder)
    {
        builder.ToTable("device_configurations");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.DeviceId).IsUnique();
        builder.Property(x => x.CapabilitiesJson).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
        builder.HasOne(x => x.Device).WithOne(x => x.Configuration).HasForeignKey<DeviceConfiguration>(x => x.DeviceId);
    }
}

public sealed class DeviceProvisioningConfiguration : IEntityTypeConfiguration<DeviceProvisioning>
{
    public void Configure(EntityTypeBuilder<DeviceProvisioning> builder)
    {
        builder.ToTable("device_provisionings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProvisioningCodeHash).HasMaxLength(512).IsRequired();
        builder.HasIndex(x => x.ProvisioningCodeHash).IsUnique();
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.DeviceId);
        builder.HasIndex(x => x.CreatedAt);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.ActivatedFromIp).HasMaxLength(64);
        builder.HasOne(x => x.Device).WithMany(x => x.Provisionings).HasForeignKey(x => x.DeviceId);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CommandConfiguration : IEntityTypeConfiguration<Command>
{
    public void Configure(EntityTypeBuilder<Command> builder)
    {
        builder.ToTable("commands");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128);
        builder.Property(x => x.Payload).HasMaxLength(4000);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.HasIndex(x => new { x.DeviceId, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");
        builder.HasIndex(x => new { x.DeviceId, x.Status });
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.DeviceId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });
        builder.HasIndex(x => x.CreatedAt);
        builder.HasOne(x => x.Device).WithMany(x => x.Commands).HasForeignKey(x => x.DeviceId);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DeviceEventConfiguration : IEntityTypeConfiguration<DeviceEvent>
{
    public void Configure(EntityTypeBuilder<DeviceEvent> builder)
    {
        builder.ToTable("device_events");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Result).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Origin).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.Property(x => x.Details).HasMaxLength(2000);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.DeviceId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });
        builder.HasIndex(x => x.CreatedAt);
        builder.HasOne(x => x.Device).WithMany(x => x.Events).HasForeignKey(x => x.DeviceId);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SecurityAuditLogConfiguration : IEntityTypeConfiguration<SecurityAuditLog>
{
    public void Configure(EntityTypeBuilder<SecurityAuditLog> builder)
    {
        builder.ToTable("security_audit_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasConversion<string>().HasMaxLength(64);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.Property(x => x.Details).HasMaxLength(2000);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.CreatedAt);
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.FamilyId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.CreatedAt);
        builder.Property(x => x.DeviceInfo).HasMaxLength(256);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.HasOne(x => x.User).WithMany(x => x.RefreshTokens).HasForeignKey(x => x.UserId);
    }
}

public sealed class DevicePermissionConfiguration : IEntityTypeConfiguration<DevicePermission>
{
    public void Configure(EntityTypeBuilder<DevicePermission> builder)
    {
        builder.ToTable("device_permissions");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.UserId, x.DeviceId }).IsUnique();
        builder.HasIndex(x => x.DeviceId);
        builder.HasIndex(x => x.UserId);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        builder.HasOne(x => x.Device).WithMany(x => x.Permissions).HasForeignKey(x => x.DeviceId);
    }
}

public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.UsedAt });
        builder.HasIndex(x => x.ExpiresAt);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnType("jsonb");
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => new { x.UserId, x.ReadAt });
        builder.HasIndex(x => x.CreatedAt);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("notification_deliveries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Channel).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Error).HasMaxLength(1000);
        builder.HasOne(x => x.Notification).WithMany(x => x.Deliveries).HasForeignKey(x => x.NotificationId);
    }
}

public sealed class UserDeviceNotificationPreferenceConfiguration
    : IEntityTypeConfiguration<UserDeviceNotificationPreference>
{
    public void Configure(EntityTypeBuilder<UserDeviceNotificationPreference> builder)
    {
        builder.ToTable("user_device_notification_preferences");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.UserId, x.DeviceId }).IsUnique();
        builder.HasIndex(x => x.DeviceId);
        builder.HasIndex(x => x.TenantId);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class UserPushTokenConfiguration : IEntityTypeConfiguration<UserPushToken>
{
    public void Configure(EntityTypeBuilder<UserPushToken> builder)
    {
        builder.ToTable("user_push_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Token).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Platform).HasMaxLength(32).IsRequired();
        builder.Property(x => x.DeviceName).HasMaxLength(200);
        builder.HasIndex(x => x.Token).IsUnique();
        builder.HasIndex(x => x.UserId);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

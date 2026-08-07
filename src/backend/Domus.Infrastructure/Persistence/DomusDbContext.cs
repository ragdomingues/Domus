using System.Linq.Expressions;
using Domus.Application.Abstractions;
using Domus.Domain.Common;
using Domus.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Domus.Infrastructure.Persistence;

public sealed class DomusDbContext : DbContext, IDomusDbContext
{
    public DomusDbContext(DbContextOptions<DomusDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<Residence> Residences => Set<Residence>();
    public DbSet<ResidenceMembership> ResidenceMemberships => Set<ResidenceMembership>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Gate> Gates => Set<Gate>();
    public DbSet<DeviceConfiguration> DeviceConfigurations => Set<DeviceConfiguration>();
    public DbSet<DeviceProvisioning> DeviceProvisionings => Set<DeviceProvisioning>();
    public DbSet<Command> Commands => Set<Command>();
    public DbSet<DeviceEvent> DeviceEvents => Set<DeviceEvent>();
    public DbSet<SecurityAuditLog> SecurityAuditLogs => Set<SecurityAuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<DevicePermission> DevicePermissions => Set<DevicePermission>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<UserDeviceNotificationPreference> UserDeviceNotificationPreferences => Set<UserDeviceNotificationPreference>();
    public DbSet<UserPushToken> UserPushTokens => Set<UserPushToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DomusDbContext).Assembly);
        ApplySoftDeleteFilters(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    private static void ApplySoftDeleteFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(SoftDeletableEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var method = typeof(DomusDbContext)
                .GetMethod(nameof(SetSoftDeleteFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(entityType.ClrType);

            method.Invoke(null, [modelBuilder]);
        }
    }

    private static void SetSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : SoftDeletableEntity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.DeletedAt == null);
    }
}

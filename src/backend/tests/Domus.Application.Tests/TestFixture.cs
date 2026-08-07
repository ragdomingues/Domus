using Domus.Application.Abstractions;
using Domus.Application.Auth;
using Domus.Application.Devices;
using Domus.Application.Notifications;
using Domus.Application.Residences;
using Domus.Application.Security;
using Domus.Domain.Entities;
using Domus.Domain.Enums;
using Domus.Infrastructure.Persistence;
using Domus.Infrastructure.Security;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Domus.Application.Tests;

internal static class TestFixture
{
    public static DomusDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<DomusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DomusDbContext(options);
    }

    public static IServiceProvider CreateServices(DomusDbContext db)
    {
        var services = new ServiceCollection();
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
        services.AddSingleton<IDomusDbContext>(db);
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<ISecretHasher, Sha256SecretHasher>();
        services.AddSingleton<ISecureTokenGenerator, SecureTokenGenerator>();
        services.AddSingleton<IAccessControlService, AccessControlService>();
        services.AddSingleton<IActivateAbuseGuard, ActivateAbuseGuard>();
        return services.BuildServiceProvider();
    }

    public static async Task<SeedData> SeedAsync(
        DomusDbContext db,
        ResidenceRole residenceRole = ResidenceRole.Administrator,
        TenantRole tenantRole = TenantRole.Owner)
    {
        var user = User.Create($"{Guid.NewGuid():N}@test.com", "hash", "User");
        var slug = $"tenant-{Guid.NewGuid():N}";
        var tenant = Tenant.Create("Tenant", slug.Length <= 40 ? slug : slug[..40]);
        var residence = Residence.Create(tenant.Id, "Casa", "America/Sao_Paulo");

        db.Users.Add(user);
        db.Tenants.Add(tenant);
        db.Residences.Add(residence);
        db.TenantMemberships.Add(TenantMembership.Create(user.Id, tenant.Id, tenantRole));
        db.ResidenceMemberships.Add(ResidenceMembership.Create(user.Id, residence.Id, residenceRole));
        await db.SaveChangesAsync();

        return new SeedData(user, tenant, residence);
    }

    public static DeviceService CreateDeviceService(DomusDbContext db, IServiceProvider? sp = null)
    {
        sp ??= CreateServices(db);
        return new DeviceService(
            db,
            sp.GetRequiredService<IAccessControlService>(),
            new CapturingDeviceMessenger(),
            sp.GetRequiredService<IValidator<CreateDeviceRequest>>(),
            sp.GetRequiredService<IValidator<UpdateDeviceRequest>>(),
            NullLogger<DeviceService>.Instance);
    }

    public static ProvisioningService CreateProvisioningService(
        DomusDbContext db,
        IDateTimeProvider? clock = null,
        IServiceProvider? sp = null)
    {
        sp ??= CreateServices(db);
        return new ProvisioningService(
            db,
            sp.GetRequiredService<IAccessControlService>(),
            sp.GetRequiredService<ISecretHasher>(),
            sp.GetRequiredService<ISecureTokenGenerator>(),
            clock ?? sp.GetRequiredService<IDateTimeProvider>(),
            sp.GetRequiredService<IActivateAbuseGuard>(),
            sp.GetRequiredService<IValidator<IssueProvisioningRequest>>(),
            sp.GetRequiredService<IValidator<ActivateProvisioningRequest>>());
    }

    public static CommandService CreateCommandService(
        DomusDbContext db,
        IDeviceMessenger? messenger = null,
        IDateTimeProvider? clock = null,
        IServiceProvider? sp = null)
    {
        sp ??= CreateServices(db);
        clock ??= sp.GetRequiredService<IDateTimeProvider>();
        var realtime = NullDeviceRealtimeNotifier.Instance;
        var simulation = new DeviceSimulationService(
            db,
            sp.GetRequiredService<IAccessControlService>(),
            sp.GetRequiredService<ISecureTokenGenerator>(),
            sp.GetRequiredService<ISecretHasher>(),
            clock,
            realtime,
            new GateNotificationService(
                db,
                clock,
                realtime,
                NullPushNotificationSender.Instance,
                NullLogger<GateNotificationService>.Instance),
            CreateProvisioningService(db, clock, sp),
            NullLogger<DeviceSimulationService>.Instance);

        return new CommandService(
            db,
            sp.GetRequiredService<IAccessControlService>(),
            messenger ?? new CapturingDeviceMessenger(),
            new CommandIdempotencyService(db),
            realtime,
            simulation,
            clock,
            Microsoft.Extensions.Options.Options.Create(new HistoryRetentionOptions()),
            sp.GetRequiredService<IValidator<CreateCommandRequest>>(),
            NullLogger<CommandService>.Instance);
    }

    public static async Task<Device> CreateActiveDeviceAsync(DomusDbContext db, SeedData seed, string name = "Portão")
    {
        var devices = CreateDeviceService(db);
        var created = await devices.CreateAsync(
            new CreateDeviceRequest(seed.Residence.Id, DeviceType.Gate, name),
            new DeviceActorContext(seed.User.Id, null, null));

        var device = await db.Devices.FirstAsync(d => d.Id == created.Value!.Id);
        device.ActivateMqttCredentials($"dev_{device.Id:N}"[..20], "hash");
        await db.SaveChangesAsync();
        return device;
    }

    public sealed record SeedData(User User, Tenant Tenant, Residence Residence);

    public sealed class FakeClock : IDateTimeProvider
    {
        public FakeClock(DateTimeOffset utcNow) => UtcNow = utcNow;
        public DateTimeOffset UtcNow { get; set; }
    }

    public sealed class CapturingDeviceMessenger : IDeviceMessenger
    {
        public List<(Guid TenantId, Guid DeviceId, Guid CommandId, Guid CorrelationId, string Action)> Published { get; } = [];

        public Task PublishCommandAsync(
            Guid tenantId,
            Guid deviceId,
            Guid commandId,
            Guid correlationId,
            string action,
            DateTimeOffset issuedAt,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken = default)
        {
            Published.Add((tenantId, deviceId, commandId, correlationId, action));
            return Task.CompletedTask;
        }

        public Task PublishConfigurationAsync(
            Guid tenantId,
            Guid deviceId,
            string configurationJson,
            CancellationToken cancellationToken = default,
            bool retain = true) =>
            Task.CompletedTask;
    }

    public sealed class FailingDeviceMessenger : IDeviceMessenger
    {
        public Task PublishCommandAsync(
            Guid tenantId,
            Guid deviceId,
            Guid commandId,
            Guid correlationId,
            string action,
            DateTimeOffset issuedAt,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("mqtt_unavailable");

        public Task PublishConfigurationAsync(
            Guid tenantId,
            Guid deviceId,
            string configurationJson,
            CancellationToken cancellationToken = default,
            bool retain = true) =>
            Task.CompletedTask;
    }
}

using Domus.Application.Abstractions;
using Domus.Application.Security;
using Domus.Domain.Entities;
using Domus.Domain.Enums;
using Domus.Infrastructure.Persistence;
using Domus.Infrastructure.Security;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Domus.Application.Tests;

public class AccessControlServiceTests
{
    [Fact]
    public async Task EnsureCanAccessResidence_denies_user_without_membership()
    {
        await using var db = CreateDb();
        var (userA, _, residence) = await SeedTenantAsync(db, "a@test.com", "Tenant A");
        var (userB, _, _) = await SeedTenantAsync(db, "b@test.com", "Tenant B");

        var access = CreateAccess(db);
        var result = await access.EnsureCanAccessResidenceAsync(userB.Id, residence.Id);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be("access_denied");

        var audits = await db.SecurityAuditLogs
            .Where(a => a.Action == SecurityAuditAction.IdorBlocked)
            .CountAsync();
        audits.Should().Be(1);
        _ = userA;
    }

    [Fact]
    public async Task EnsureCanAccessResidence_denies_expired_visitor()
    {
        await using var db = CreateDb();
        var clock = new FakeClock(DateTimeOffset.Parse("2026-08-06T18:00:00Z"));
        var (admin, tenant, residence) = await SeedTenantAsync(db, "admin@test.com", "Tenant");
        var visitor = User.Create("visitor@test.com", "hash", "Visitor");
        db.Users.Add(visitor);
        db.ResidenceMemberships.Add(ResidenceMembership.Create(
            visitor.Id,
            residence.Id,
            ResidenceRole.Visitor,
            validFrom: clock.UtcNow.AddDays(-2),
            validUntil: clock.UtcNow.AddHours(-1)));
        await db.SaveChangesAsync();

        var access = new AccessControlService(db, clock);
        var result = await access.EnsureCanAccessResidenceAsync(visitor.Id, residence.Id);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be("access_denied");
        _ = admin;
        _ = tenant;
    }

    [Fact]
    public async Task EnsureCanAccessDevice_denies_cross_tenant_user()
    {
        await using var db = CreateDb();
        var (userA, tenantA, residenceA) = await SeedTenantAsync(db, "a@test.com", "Tenant A");
        var (userB, _, _) = await SeedTenantAsync(db, "b@test.com", "Tenant B");

        var device = Device.Create(tenantA.Id, residenceA.Id, DeviceType.Gate, "Portão");
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        var access = CreateAccess(db);
        var result = await access.EnsureCanAccessDeviceAsync(userB.Id, device.Id);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be("access_denied");
        _ = userA;
    }

    [Fact]
    public async Task EnsureCanAccessResidence_allows_active_member()
    {
        await using var db = CreateDb();
        var (user, _, residence) = await SeedTenantAsync(db, "ok@test.com", "Tenant");
        var access = CreateAccess(db);

        var result = await access.EnsureCanAccessResidenceAsync(user.Id, residence.Id);
        result.Succeeded.Should().BeTrue();
    }

    private static AccessControlService CreateAccess(DomusDbContext db) =>
        new(db, new SystemDateTimeProvider());

    private static DomusDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<DomusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DomusDbContext(options);
    }

    private static async Task<(User user, Tenant tenant, Residence residence)> SeedTenantAsync(
        DomusDbContext db,
        string email,
        string tenantName)
    {
        var user = User.Create(email, "hash", "User");
        var slugBase = $"{tenantName.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}";
        var tenant = Tenant.Create(tenantName, slugBase.Length <= 40 ? slugBase : slugBase[..40]);
        var residence = Residence.Create(tenant.Id, "Casa", "America/Sao_Paulo");
        db.Users.Add(user);
        db.Tenants.Add(tenant);
        db.Residences.Add(residence);
        db.TenantMemberships.Add(TenantMembership.Create(user.Id, tenant.Id, TenantRole.Owner));
        db.ResidenceMemberships.Add(ResidenceMembership.Create(user.Id, residence.Id, ResidenceRole.Administrator));
        await db.SaveChangesAsync();
        return (user, tenant, residence);
    }

    private sealed class FakeClock : IDateTimeProvider
    {
        public FakeClock(DateTimeOffset utcNow) => UtcNow = utcNow;
        public DateTimeOffset UtcNow { get; }
    }
}

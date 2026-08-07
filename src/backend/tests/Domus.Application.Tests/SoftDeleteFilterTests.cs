using Domus.Domain.Entities;
using Domus.Domain.Enums;
using Domus.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Domus.Application.Tests;

public class SoftDeleteFilterTests
{
    [Fact]
    public async Task Soft_deleted_user_is_filtered_from_default_queries()
    {
        await using var db = CreateDb();
        var user = User.Create("soft@test.com", "hash", "Soft");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        user.SoftDelete(Guid.NewGuid());
        await db.SaveChangesAsync();

        (await db.Users.CountAsync()).Should().Be(0);
        (await db.Users.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Soft_deleted_device_is_filtered_from_default_queries()
    {
        await using var db = CreateDb();
        var tenant = Tenant.Create("T", $"t-{Guid.NewGuid():N}"[..20]);
        var residence = Residence.Create(tenant.Id, "Casa", "America/Sao_Paulo");
        var device = Device.Create(tenant.Id, residence.Id, DeviceType.Gate, "Portão");
        db.Tenants.Add(tenant);
        db.Residences.Add(residence);
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        device.SoftDelete(Guid.NewGuid());
        await db.SaveChangesAsync();

        (await db.Devices.CountAsync()).Should().Be(0);
        (await db.Devices.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    }

    private static DomusDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<DomusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DomusDbContext(options);
    }
}

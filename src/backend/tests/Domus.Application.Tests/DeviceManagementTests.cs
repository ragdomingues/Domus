using Domus.Application.Devices;
using Domus.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Domus.Application.Tests;

public class DeviceManagementTests
{
    [Fact]
    public async Task Member_cannot_create_device()
    {
        await using var db = TestFixture.CreateDb();
        var seed = await TestFixture.SeedAsync(db, ResidenceRole.Member);
        var devices = TestFixture.CreateDeviceService(db);

        var result = await devices.CreateAsync(
            new CreateDeviceRequest(seed.Residence.Id, DeviceType.Gate, "Portão"),
            new DeviceActorContext(seed.User.Id, "127.0.0.1", "test"));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be("forbidden");
        (await db.Devices.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Administrator_creates_gate_with_configuration()
    {
        await using var db = TestFixture.CreateDb();
        var seed = await TestFixture.SeedAsync(db);
        var devices = TestFixture.CreateDeviceService(db);

        var result = await devices.CreateAsync(
            new CreateDeviceRequest(seed.Residence.Id, DeviceType.Gate, "Portão Garagem"),
            new DeviceActorContext(seed.User.Id, null, null));

        result.Succeeded.Should().BeTrue();
        result.Value!.Type.Should().Be(DeviceType.Gate);
        result.Value.IsProvisioned.Should().BeFalse();
        result.Value.Configuration.Should().NotBeNull();
        result.Value.GateState.Should().Be(GateState.Unknown);
        (await db.Gates.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Tenant_isolation_blocks_device_access()
    {
        await using var db = TestFixture.CreateDb();
        var tenantA = await TestFixture.SeedAsync(db);
        var tenantB = await TestFixture.SeedAsync(db);
        var devices = TestFixture.CreateDeviceService(db);

        var created = await devices.CreateAsync(
            new CreateDeviceRequest(tenantA.Residence.Id, DeviceType.Gate, "Portão A"),
            new DeviceActorContext(tenantA.User.Id, null, null));

        var access = await devices.GetAsync(created.Value!.Id, tenantB.User.Id);
        access.Succeeded.Should().BeFalse();
        access.ErrorCode.Should().Be("access_denied");
    }
}

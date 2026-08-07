using Domus.Application.Abstractions;
using Domus.Application.Devices;
using Domus.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Domus.Application.Tests;

public class ProvisioningServiceTests
{
    [Fact]
    public async Task Full_issue_and_activate_flow_returns_mqtt_credentials_once()
    {
        await using var db = TestFixture.CreateDb();
        var seed = await TestFixture.SeedAsync(db);
        var devices = TestFixture.CreateDeviceService(db);
        var provisioning = TestFixture.CreateProvisioningService(db);

        var device = await devices.CreateAsync(
            new CreateDeviceRequest(seed.Residence.Id, DeviceType.Gate, "Portão"),
            new DeviceActorContext(seed.User.Id, null, null));

        var issued = await provisioning.IssueAsync(
            new IssueProvisioningRequest(device.Value!.Id),
            new DeviceActorContext(seed.User.Id, "10.0.0.1", "admin"));

        issued.Succeeded.Should().BeTrue();
        issued.Value!.ProvisioningCode.Should().NotBeNullOrWhiteSpace();

        var activated = await provisioning.ActivateAsync(
            new ActivateProvisioningRequest(issued.Value.ProvisioningCode, "esp32-hw-1", "1.0.0"),
            "10.0.0.2");

        activated.Succeeded.Should().BeTrue();
        activated.Value!.MqttUsername.Should().StartWith("dev_");
        activated.Value.MqttPassword.Should().NotBeNullOrWhiteSpace();
        activated.Value.TopicCommand.Should().Contain("/command");

        var stored = await db.Devices.FirstAsync(d => d.Id == device.Value.Id);
        stored.HasMqttCredentials.Should().BeTrue();
        stored.MqttSecretHash.Should().NotBe(activated.Value.MqttPassword);
        stored.HardwareId.Should().Be("esp32-hw-1");

        var status = await provisioning.GetStatusAsync(issued.Value.ProvisioningId, seed.User.Id);
        status.Value!.Status.Should().Be(ProvisioningStatus.Activated);
    }

    [Fact]
    public async Task Expired_provisioning_cannot_activate()
    {
        await using var db = TestFixture.CreateDb();
        var seed = await TestFixture.SeedAsync(db);
        var clock = new TestFixture.FakeClock(DateTimeOffset.Parse("2026-08-06T12:00:00Z"));
        var devices = TestFixture.CreateDeviceService(db);
        var provisioning = TestFixture.CreateProvisioningService(db, clock);

        var device = await devices.CreateAsync(
            new CreateDeviceRequest(seed.Residence.Id, DeviceType.Gate, "Portão"),
            new DeviceActorContext(seed.User.Id, null, null));

        var issued = await provisioning.IssueAsync(
            new IssueProvisioningRequest(device.Value!.Id, ExpiresInMinutes: 10),
            new DeviceActorContext(seed.User.Id, null, null));

        clock.UtcNow = clock.UtcNow.AddHours(2);

        var activated = await provisioning.ActivateAsync(
            new ActivateProvisioningRequest(issued.Value!.ProvisioningCode, "hw"),
            null);

        activated.Succeeded.Should().BeFalse();
        activated.ErrorCode.Should().Be("provisioning_expired");
    }

    [Fact]
    public async Task Provisioning_code_cannot_be_reused()
    {
        await using var db = TestFixture.CreateDb();
        var seed = await TestFixture.SeedAsync(db);
        var devices = TestFixture.CreateDeviceService(db);
        var provisioning = TestFixture.CreateProvisioningService(db);

        var device = await devices.CreateAsync(
            new CreateDeviceRequest(seed.Residence.Id, DeviceType.Light, "Luz"),
            new DeviceActorContext(seed.User.Id, null, null));

        var issued = await provisioning.IssueAsync(
            new IssueProvisioningRequest(device.Value!.Id),
            new DeviceActorContext(seed.User.Id, null, null));

        var first = await provisioning.ActivateAsync(
            new ActivateProvisioningRequest(issued.Value!.ProvisioningCode, "hw-1"),
            null);
        first.Succeeded.Should().BeTrue();

        var second = await provisioning.ActivateAsync(
            new ActivateProvisioningRequest(issued.Value.ProvisioningCode, "hw-2"),
            null);

        second.Succeeded.Should().BeFalse();
        second.ErrorCode.Should().Be("provisioning_reused");
    }

    [Fact]
    public async Task Already_activated_device_cannot_issue_or_activate_again()
    {
        await using var db = TestFixture.CreateDb();
        var seed = await TestFixture.SeedAsync(db);
        var devices = TestFixture.CreateDeviceService(db);
        var provisioning = TestFixture.CreateProvisioningService(db);

        var device = await devices.CreateAsync(
            new CreateDeviceRequest(seed.Residence.Id, DeviceType.Gate, "Portão"),
            new DeviceActorContext(seed.User.Id, null, null));

        var issued = await provisioning.IssueAsync(
            new IssueProvisioningRequest(device.Value!.Id),
            new DeviceActorContext(seed.User.Id, null, null));

        await provisioning.ActivateAsync(
            new ActivateProvisioningRequest(issued.Value!.ProvisioningCode, "hw-1"),
            null);

        var reissue = await provisioning.IssueAsync(
            new IssueProvisioningRequest(device.Value.Id),
            new DeviceActorContext(seed.User.Id, null, null));

        reissue.Succeeded.Should().BeFalse();
        reissue.ErrorCode.Should().Be("device_already_activated");
    }

    [Fact]
    public async Task User_without_permission_cannot_issue_provisioning()
    {
        await using var db = TestFixture.CreateDb();
        var admin = await TestFixture.SeedAsync(db, ResidenceRole.Administrator);
        var member = await TestFixture.SeedAsync(db, ResidenceRole.Member);
        // Put member on admin's residence as Member
        db.ResidenceMemberships.Add(Domain.Entities.ResidenceMembership.Create(
            member.User.Id,
            admin.Residence.Id,
            ResidenceRole.Member));
        db.TenantMemberships.Add(Domain.Entities.TenantMembership.Create(
            member.User.Id,
            admin.Tenant.Id,
            TenantRole.Member));
        await db.SaveChangesAsync();

        var devices = TestFixture.CreateDeviceService(db);
        var provisioning = TestFixture.CreateProvisioningService(db);

        var device = await devices.CreateAsync(
            new CreateDeviceRequest(admin.Residence.Id, DeviceType.Gate, "Portão"),
            new DeviceActorContext(admin.User.Id, null, null));

        var issued = await provisioning.IssueAsync(
            new IssueProvisioningRequest(device.Value!.Id),
            new DeviceActorContext(member.User.Id, null, null));

        issued.Succeeded.Should().BeFalse();
        issued.ErrorCode.Should().Be("forbidden");
    }
}

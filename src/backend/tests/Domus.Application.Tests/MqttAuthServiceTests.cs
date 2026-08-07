using Domus.Application.Devices;
using Domus.Domain.Entities;
using Domus.Domain.Enums;
using Domus.Infrastructure.Security;
using FluentAssertions;

namespace Domus.Application.Tests;

public class MqttAuthServiceTests
{
    [Fact]
    public async Task Device_can_only_access_own_topics()
    {
        await using var db = TestFixture.CreateDb();
        var seed = await TestFixture.SeedAsync(db);
        var hasher = new Sha256SecretHasher();
        var password = "secret-mqtt";
        var device = Device.Create(seed.Tenant.Id, seed.Residence.Id, DeviceType.Gate, "Portão");
        device.MarkProvisioning();
        device.SetHardwareId("hw-1");
        device.ActivateMqttCredentials("dev_abc", hasher.Hash(password));
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        var auth = new MqttAuthService(db, hasher, new MqttServiceCredentials
        {
            Username = "domus_api",
            Password = "api-pass"
        });

        (await auth.AuthenticateAsync(new MqttAuthRequest(device.MqttUsername!, password))).Should().BeTrue();
        (await auth.AuthenticateAsync(new MqttAuthRequest(device.MqttUsername!, "wrong"))).Should().BeFalse();

        var ownStatus = MqttTopics.Status(device.TenantId, device.Id);
        var otherStatus = MqttTopics.Status(device.TenantId, Guid.NewGuid());
        var otherTenant = MqttTopics.Status(Guid.NewGuid(), device.Id);

        (await auth.AuthorizeAsync(new MqttAclRequest(device.MqttUsername!, ownStatus, "publish"))).Should().BeTrue();
        (await auth.AuthorizeAsync(new MqttAclRequest(device.MqttUsername!, ownStatus, "subscribe"))).Should().BeFalse();
        (await auth.AuthorizeAsync(new MqttAclRequest(device.MqttUsername!, otherStatus, "publish"))).Should().BeFalse();
        (await auth.AuthorizeAsync(new MqttAclRequest(device.MqttUsername!, otherTenant, "publish"))).Should().BeFalse();
        (await auth.AuthorizeAsync(new MqttAclRequest(device.MqttUsername!, MqttTopics.Command(device.TenantId, device.Id), "subscribe"))).Should().BeTrue();
    }

    [Fact]
    public async Task Api_service_account_can_publish_commands_and_subscribe_status()
    {
        await using var db = TestFixture.CreateDb();
        var auth = new MqttAuthService(db, new Sha256SecretHasher(), new MqttServiceCredentials
        {
            Username = "domus_api",
            Password = "api-pass"
        });

        (await auth.AuthenticateAsync(new MqttAuthRequest("domus_api", "api-pass"))).Should().BeTrue();

        var tenantId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        (await auth.AuthorizeAsync(new MqttAclRequest("domus_api", MqttTopics.Command(tenantId, deviceId), "publish"))).Should().BeTrue();
        (await auth.AuthorizeAsync(new MqttAclRequest("domus_api", MqttTopics.StatusWildcard, "subscribe"))).Should().BeTrue();
        (await auth.AuthorizeAsync(new MqttAclRequest("domus_api", MqttTopics.Status(tenantId, deviceId), "publish"))).Should().BeFalse();
    }

    [Fact]
    public async Task Suspended_device_cannot_authenticate()
    {
        await using var db = TestFixture.CreateDb();
        var seed = await TestFixture.SeedAsync(db);
        var hasher = new Sha256SecretHasher();
        var device = Device.Create(seed.Tenant.Id, seed.Residence.Id, DeviceType.Gate, "Portão");
        device.SetHardwareId("hw-susp");
        device.ActivateMqttCredentials("dev_susp", hasher.Hash("pwd"));
        device.Suspend();
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        var auth = new MqttAuthService(db, hasher, new MqttServiceCredentials());
        (await auth.AuthenticateAsync(new MqttAuthRequest("dev_susp", "pwd"))).Should().BeFalse();
    }
}

using Domus.Domain.Entities;
using Domus.Domain.Enums;
using FluentAssertions;

namespace Domus.Domain.Tests;

public class DeviceProvisioningTests
{
    [Fact]
    public void Activate_succeeds_when_pending_and_not_expired()
    {
        var provisioning = DeviceProvisioning.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hash",
            DateTimeOffset.UtcNow.AddHours(1));

        provisioning.Activate("127.0.0.1");

        provisioning.Status.Should().Be(ProvisioningStatus.Activated);
        provisioning.ActivatedAt.Should().NotBeNull();
        provisioning.ActivatedFromIp.Should().Be("127.0.0.1");
    }

    [Fact]
    public void Activate_fails_when_expired()
    {
        var provisioning = DeviceProvisioning.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "hash",
            DateTimeOffset.UtcNow.AddMinutes(-5));

        var act = () => provisioning.Activate("127.0.0.1");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Device_has_no_mqtt_credentials_until_activated()
    {
        var device = Device.Create(Guid.NewGuid(), Guid.NewGuid(), DeviceType.Gate, "Portão");
        device.HasMqttCredentials.Should().BeFalse();

        device.ActivateMqttCredentials("device-1", "secret-hash");
        device.HasMqttCredentials.Should().BeTrue();
    }
}

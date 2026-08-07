using Domus.Domain.Entities;
using Domus.Domain.Enums;
using FluentAssertions;

namespace Domus.Domain.Tests;

public class DeviceLifecycleTests
{
    [Fact]
    public void Lifecycle_progresses_created_provisioning_active()
    {
        var device = Device.Create(Guid.NewGuid(), Guid.NewGuid(), DeviceType.Gate, "Portão");
        device.LifecycleStatus.Should().Be(DeviceLifecycleStatus.Created);
        device.ConnectionStatus.Should().Be(DeviceConnectionStatus.Unknown);

        device.MarkProvisioning();
        device.LifecycleStatus.Should().Be(DeviceLifecycleStatus.Provisioning);

        device.ActivateMqttCredentials("dev_x", "hash");
        device.LifecycleStatus.Should().Be(DeviceLifecycleStatus.Active);

        device.MarkOnline("1.0.0");
        device.ConnectionStatus.Should().Be(DeviceConnectionStatus.Online);

        device.SoftDelete(Guid.NewGuid());
        device.LifecycleStatus.Should().Be(DeviceLifecycleStatus.Deleted);
        device.ConnectionStatus.Should().Be(DeviceConnectionStatus.Offline);
    }
}

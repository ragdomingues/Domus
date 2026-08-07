using Domus.Application.Realtime;
using Domus.Domain.Enums;
using FluentAssertions;

namespace Domus.Application.Tests;

public class RealtimePayloadTests
{
    [Fact]
    public void All_v1_payloads_include_schema_version_1()
    {
        DeviceStatusChangedPayload.Create(Guid.NewGuid(), DeviceConnectionStatus.Online, GateState.Closed, DateTimeOffset.UtcNow)
            .SchemaVersion.Should().Be(1);

        GateStateChangedPayload.Create(Guid.NewGuid(), GateState.Open, DateTimeOffset.UtcNow)
            .SchemaVersion.Should().Be(1);

        CommandUpdatedPayload.Create(Guid.NewGuid(), Guid.NewGuid(), CommandStatus.Sent, CommandAction.Open, null)
            .SchemaVersion.Should().Be(1);

        DeviceOfflinePayload.Create(Guid.NewGuid(), DateTimeOffset.UtcNow)
            .SchemaVersion.Should().Be(1);

        RealtimeContract.SchemaVersion.Should().Be(1);
    }
}

using Domus.Application.Devices;
using Domus.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Domus.Application.Tests;

public class CommandServiceTests
{
    [Fact]
    public async Task Create_publishes_and_marks_sent_with_correlationId()
    {
        await using var db = TestFixture.CreateDb();
        var seed = await TestFixture.SeedAsync(db);
        var device = await TestFixture.CreateActiveDeviceAsync(db, seed);
        var messenger = new TestFixture.CapturingDeviceMessenger();
        var commands = TestFixture.CreateCommandService(db, messenger);

        var result = await commands.CreateAsync(
            new CreateCommandRequest(device.Id, CommandAction.Open),
            new DeviceActorContext(seed.User.Id, "10.0.0.1", "app"));

        result.Succeeded.Should().BeTrue();
        result.Value!.Status.Should().Be(CommandStatus.Sent);
        result.Value.CorrelationId.Should().NotBeEmpty();
        result.Value.AttemptCount.Should().Be(1);
        messenger.Published.Should().HaveCount(1);
        messenger.Published[0].CommandId.Should().Be(result.Value.Id);
        messenger.Published[0].CorrelationId.Should().Be(result.Value.CorrelationId);

        (await db.SecurityAuditLogs.CountAsync(a => a.Action == SecurityAuditAction.CommandCreated))
            .Should().Be(1);
        (await db.DeviceEvents.CountAsync(e => e.CommandId == result.Value.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Idempotency_key_returns_same_command()
    {
        await using var db = TestFixture.CreateDb();
        var seed = await TestFixture.SeedAsync(db);
        var device = await TestFixture.CreateActiveDeviceAsync(db, seed);
        var messenger = new TestFixture.CapturingDeviceMessenger();
        var commands = TestFixture.CreateCommandService(db, messenger);

        var first = await commands.CreateAsync(
            new CreateCommandRequest(device.Id, CommandAction.Open, "idem-1"),
            new DeviceActorContext(seed.User.Id, null, null));
        var second = await commands.CreateAsync(
            new CreateCommandRequest(device.Id, CommandAction.Open, "idem-1"),
            new DeviceActorContext(seed.User.Id, null, null));

        first.Succeeded.Should().BeTrue();
        second.Succeeded.Should().BeTrue();
        second.Value!.Id.Should().Be(first.Value!.Id);
        messenger.Published.Should().HaveCount(1);
        (await db.Commands.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Inactive_device_cannot_receive_commands()
    {
        await using var db = TestFixture.CreateDb();
        var seed = await TestFixture.SeedAsync(db);
        var devices = TestFixture.CreateDeviceService(db);
        var created = await devices.CreateAsync(
            new CreateDeviceRequest(seed.Residence.Id, DeviceType.Gate, "Portão"),
            new DeviceActorContext(seed.User.Id, null, null));

        var commands = TestFixture.CreateCommandService(db);
        var result = await commands.CreateAsync(
            new CreateCommandRequest(created.Value!.Id, CommandAction.Open),
            new DeviceActorContext(seed.User.Id, null, null));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be("device_not_active");
    }

    [Fact]
    public async Task Publish_failure_schedules_retry_then_fails()
    {
        await using var db = TestFixture.CreateDb();
        var seed = await TestFixture.SeedAsync(db);
        var device = await TestFixture.CreateActiveDeviceAsync(db, seed);
        var clock = new TestFixture.FakeClock(DateTimeOffset.Parse("2026-08-06T12:00:00Z"));
        var commands = TestFixture.CreateCommandService(db, new TestFixture.FailingDeviceMessenger(), clock);

        var created = await commands.CreateAsync(
            new CreateCommandRequest(device.Id, CommandAction.Open, TimeoutSeconds: 120),
            new DeviceActorContext(seed.User.Id, null, null));

        created.Succeeded.Should().BeTrue();
        created.Value!.Status.Should().Be(CommandStatus.Pending);
        created.Value.AttemptCount.Should().Be(1);
        created.Value.NextRetryAt.Should().NotBeNull();

        clock.UtcNow = created.Value.NextRetryAt!.Value;
        await commands.ProcessDueCommandsAsync();
        var afterRetry = await commands.GetAsync(created.Value.Id, seed.User.Id);
        afterRetry.Value!.AttemptCount.Should().Be(2);
        afterRetry.Value.Status.Should().Be(CommandStatus.Pending);

        clock.UtcNow = afterRetry.Value.NextRetryAt!.Value;
        await commands.ProcessDueCommandsAsync();
        var afterThird = await commands.GetAsync(created.Value.Id, seed.User.Id);
        afterThird.Value!.AttemptCount.Should().Be(3);
        afterThird.Value.Status.Should().Be(CommandStatus.Failed);

        (await db.SecurityAuditLogs.CountAsync(a => a.Action == SecurityAuditAction.CommandFailed))
            .Should().Be(1);
    }

    [Fact]
    public async Task ProcessDue_expires_timed_out_commands()
    {
        await using var db = TestFixture.CreateDb();
        var seed = await TestFixture.SeedAsync(db);
        var device = await TestFixture.CreateActiveDeviceAsync(db, seed);
        var clock = new TestFixture.FakeClock(DateTimeOffset.Parse("2026-08-06T12:00:00Z"));
        var commands = TestFixture.CreateCommandService(db, new TestFixture.CapturingDeviceMessenger(), clock);

        var created = await commands.CreateAsync(
            new CreateCommandRequest(device.Id, CommandAction.Open, TimeoutSeconds: 10),
            new DeviceActorContext(seed.User.Id, null, null));

        created.Value!.Status.Should().Be(CommandStatus.Sent);

        clock.UtcNow = clock.UtcNow.AddSeconds(11);
        await commands.ProcessDueCommandsAsync();

        var expired = await commands.GetAsync(created.Value.Id, seed.User.Id);
        expired.Value!.Status.Should().Be(CommandStatus.Expired);
    }

    [Fact]
    public async Task Tenant_isolation_blocks_command_access()
    {
        await using var db = TestFixture.CreateDb();
        var tenantA = await TestFixture.SeedAsync(db);
        var tenantB = await TestFixture.SeedAsync(db);
        var device = await TestFixture.CreateActiveDeviceAsync(db, tenantA);
        var commands = TestFixture.CreateCommandService(db);

        var created = await commands.CreateAsync(
            new CreateCommandRequest(device.Id, CommandAction.Open),
            new DeviceActorContext(tenantA.User.Id, null, null));

        var denied = await commands.GetAsync(created.Value!.Id, tenantB.User.Id);
        denied.Succeeded.Should().BeFalse();
        denied.ErrorCode.Should().Be("access_denied");
    }

    [Fact]
    public async Task Open_in_flight_blocks_close()
    {
        await using var db = TestFixture.CreateDb();
        var seed = await TestFixture.SeedAsync(db);
        var device = await TestFixture.CreateActiveDeviceAsync(db, seed);
        var commands = TestFixture.CreateCommandService(db);

        var open = await commands.CreateAsync(
            new CreateCommandRequest(device.Id, CommandAction.Open),
            new DeviceActorContext(seed.User.Id, null, null));
        open.Succeeded.Should().BeTrue();

        var close = await commands.CreateAsync(
            new CreateCommandRequest(device.Id, CommandAction.Close),
            new DeviceActorContext(seed.User.Id, null, null));

        close.Succeeded.Should().BeFalse();
        close.ErrorCode.Should().Be("command_conflict");
    }

    [Fact]
    public async Task Stop_allowed_while_open_in_flight()
    {
        await using var db = TestFixture.CreateDb();
        var seed = await TestFixture.SeedAsync(db);
        var device = await TestFixture.CreateActiveDeviceAsync(db, seed);
        var config = await db.DeviceConfigurations.FirstAsync(c => c.DeviceId == device.Id);
        config.Update(500, 30, 30, 15, true, true, "{}", seed.User.Id);
        await db.SaveChangesAsync();

        var commands = TestFixture.CreateCommandService(db);
        var open = await commands.CreateAsync(
            new CreateCommandRequest(device.Id, CommandAction.Open),
            new DeviceActorContext(seed.User.Id, null, null));
        open.Succeeded.Should().BeTrue();

        var stop = await commands.CreateAsync(
            new CreateCommandRequest(device.Id, CommandAction.Stop),
            new DeviceActorContext(seed.User.Id, null, null));

        stop.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Open_rejected_when_gate_already_open()
    {
        await using var db = TestFixture.CreateDb();
        var seed = await TestFixture.SeedAsync(db);
        var device = await TestFixture.CreateActiveDeviceAsync(db, seed);
        var gate = await db.Gates.FirstAsync(g => g.DeviceId == device.Id);
        gate.UpdateState(GateState.Open);
        await db.SaveChangesAsync();

        var commands = TestFixture.CreateCommandService(db);
        var result = await commands.CreateAsync(
            new CreateCommandRequest(device.Id, CommandAction.Open),
            new DeviceActorContext(seed.User.Id, null, null));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be("gate_already_open");
    }

    [Fact]
    public async Task Create_persists_command_source()
    {
        await using var db = TestFixture.CreateDb();
        var seed = await TestFixture.SeedAsync(db);
        var device = await TestFixture.CreateActiveDeviceAsync(db, seed);
        var commands = TestFixture.CreateCommandService(db);

        var result = await commands.CreateAsync(
            new CreateCommandRequest(device.Id, CommandAction.Open, Source: CommandSource.WebAdmin),
            new DeviceActorContext(seed.User.Id, null, null));

        result.Succeeded.Should().BeTrue();
        result.Value!.Source.Should().Be(CommandSource.WebAdmin);
    }
}

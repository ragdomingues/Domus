using Domus.Domain.Entities;
using Domus.Domain.Enums;
using FluentAssertions;

namespace Domus.Domain.Tests;

public class CommandLifecycleTests
{
    [Fact]
    public void Command_follows_pending_sent_delivered_executed()
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(1);
        var command = Command.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            CommandAction.Open,
            expires,
            idempotencyKey: "key-1");

        command.MarkSent();
        command.Status.Should().Be(CommandStatus.Sent);

        command.MarkDelivered();
        command.Status.Should().Be(CommandStatus.Delivered);

        command.MarkExecuted();
        command.Status.Should().Be(CommandStatus.Executed);
    }

    [Fact]
    public void Command_expires_when_past_expiresAt()
    {
        var command = Command.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            CommandAction.Close,
            DateTimeOffset.UtcNow.AddMilliseconds(-1));

        var act = () => command.MarkSent();
        act.Should().Throw<InvalidOperationException>();
        command.Status.Should().Be(CommandStatus.Expired);
    }

    [Fact]
    public void TryScheduleRetry_fails_after_max_attempts()
    {
        var command = Command.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            CommandAction.Open,
            DateTimeOffset.UtcNow.AddMinutes(5));

        command.MarkSent();
        command.TryScheduleRetry(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1)).Should().BeTrue();

        command.MarkSent();
        command.TryScheduleRetry(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1)).Should().BeTrue();

        command.MarkSent();
        command.TryScheduleRetry(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1)).Should().BeFalse();
        command.Status.Should().Be(CommandStatus.Expired);
    }

    [Fact]
    public void RegisterFailedSendAttempt_retries_then_fails()
    {
        var now = DateTimeOffset.UtcNow;
        var command = Command.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            CommandAction.Open,
            now.AddMinutes(5));

        command.RegisterFailedSendAttempt("fail-1", now, TimeSpan.FromSeconds(5)).Should().BeTrue();
        command.Status.Should().Be(CommandStatus.Pending);
        command.AttemptCount.Should().Be(1);
        command.NextRetryAt.Should().Be(now.AddSeconds(5));

        command.RegisterFailedSendAttempt("fail-2", now.AddSeconds(5), TimeSpan.FromSeconds(5)).Should().BeTrue();
        command.RegisterFailedSendAttempt("fail-3", now.AddSeconds(10), TimeSpan.FromSeconds(5)).Should().BeFalse();
        command.Status.Should().Be(CommandStatus.Failed);
        command.AttemptCount.Should().Be(3);
    }

    [Fact]
    public void CorrelationId_is_assigned_on_create()
    {
        var command = Command.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            CommandAction.Stop,
            DateTimeOffset.UtcNow.AddMinutes(1));

        command.CorrelationId.Should().NotBeEmpty();
    }
}

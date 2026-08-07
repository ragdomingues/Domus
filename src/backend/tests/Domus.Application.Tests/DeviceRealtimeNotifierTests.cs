using Domus.Application.Abstractions;
using Domus.Application.Devices;
using Domus.Application.Notifications;
using Domus.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Domus.Application.Tests;

public class DeviceRealtimeNotifierTests
{
    [Fact]
    public async Task Telemetry_status_emits_gate_and_command_events()
    {
        await using var db = TestFixture.CreateDb();
        var seed = await TestFixture.SeedAsync(db);
        var device = await TestFixture.CreateActiveDeviceAsync(db, seed);
        var commands = TestFixture.CreateCommandService(db);
        var created = await commands.CreateAsync(
            new CreateCommandRequest(device.Id, CommandAction.Open),
            new DeviceActorContext(seed.User.Id, null, null));

        var notifier = new CapturingRealtimeNotifier();
        var clock = new TestFixture.FakeClock(DateTimeOffset.UtcNow);
        var gateNotifications = new GateNotificationService(
            db,
            clock,
            notifier,
            NullPushNotificationSender.Instance,
            NullLogger<GateNotificationService>.Instance);
        var telemetry = new DeviceTelemetryService(
            db,
            clock,
            notifier,
            gateNotifications,
            NullLogger<DeviceTelemetryService>.Instance);

        var topic = MqttTopics.Status(device.TenantId, device.Id);
        var payload = $$"""
            {"messageId":"{{Guid.NewGuid()}}","state":"OPEN","commandId":"{{created.Value!.Id}}","reportedAt":"2026-08-06T12:00:05Z"}
            """;

        await telemetry.HandleIncomingAsync(topic, payload);

        notifier.GateEvents.Should().ContainSingle(e => e.DeviceId == device.Id && e.State == GateState.Open);
        notifier.CommandEvents.Should().ContainSingle(e => e.CommandId == created.Value.Id && e.Status == CommandStatus.Executed);
        notifier.StatusEvents.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Presence_marks_stale_online_device_offline()
    {
        await using var db = TestFixture.CreateDb();
        var seed = await TestFixture.SeedAsync(db);
        var device = await TestFixture.CreateActiveDeviceAsync(db, seed);
        var clock = new TestFixture.FakeClock(DateTimeOffset.Parse("2026-08-06T12:00:00Z"));
        device.MarkOnline("1.0.0", clock.UtcNow);
        await db.SaveChangesAsync();

        clock.UtcNow = clock.UtcNow.AddMinutes(5);
        var notifier = new CapturingRealtimeNotifier();
        var presence = new DevicePresenceService(db, clock, notifier, NullLogger<DevicePresenceService>.Instance);

        await presence.MarkStaleDevicesOfflineAsync();

        (await db.Devices.FindAsync(device.Id))!.ConnectionStatus.Should().Be(DeviceConnectionStatus.Offline);
        notifier.OfflineEvents.Should().ContainSingle(e => e.DeviceId == device.Id);
    }

    private sealed class CapturingRealtimeNotifier : IDeviceRealtimeNotifier
    {
        public List<(Guid DeviceId, DeviceConnectionStatus Status)> StatusEvents { get; } = [];
        public List<(Guid DeviceId, GateState State)> GateEvents { get; } = [];
        public List<(Guid CommandId, CommandStatus Status)> CommandEvents { get; } = [];
        public List<(Guid DeviceId, DateTimeOffset? LastSeen)> OfflineEvents { get; } = [];

        public Task NotifyDeviceStatusChangedAsync(
            Guid tenantId, Guid residenceId, Guid deviceId, DeviceConnectionStatus connectionStatus,
            GateState? gateState, DateTimeOffset reportedAt, CancellationToken cancellationToken = default)
        {
            StatusEvents.Add((deviceId, connectionStatus));
            return Task.CompletedTask;
        }

        public Task NotifyGateStateChangedAsync(
            Guid tenantId, Guid residenceId, Guid deviceId, GateState gateState,
            DateTimeOffset reportedAt, CancellationToken cancellationToken = default)
        {
            GateEvents.Add((deviceId, gateState));
            return Task.CompletedTask;
        }

        public Task NotifyCommandUpdatedAsync(
            Guid tenantId, Guid residenceId, Guid commandId, Guid deviceId, CommandStatus status,
            CommandAction action, string? failureReason, CancellationToken cancellationToken = default)
        {
            CommandEvents.Add((commandId, status));
            return Task.CompletedTask;
        }

        public Task NotifyDeviceOfflineAsync(
            Guid tenantId, Guid residenceId, Guid deviceId, DateTimeOffset? lastSeenAt,
            CancellationToken cancellationToken = default)
        {
            OfflineEvents.Add((deviceId, lastSeenAt));
            return Task.CompletedTask;
        }

        public Task NotifyUserNotificationCreatedAsync(
            Guid userId,
            string type,
            string title,
            string body,
            Guid? deviceId,
            DateTimeOffset createdAt,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

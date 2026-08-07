using System.Reflection;
using Domus.Application.Devices;
using Domus.Domain.Common;
using Domus.Domain.Entities;
using Domus.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Domus.Application.Tests;

public sealed class HistoryRetentionServiceTests
{
    [Fact]
    public async Task Purge_removes_commands_and_events_older_than_retention()
    {
        await using var db = TestFixture.CreateDb();
        var seed = await TestFixture.SeedAsync(db);
        var device = await TestFixture.CreateActiveDeviceAsync(db, seed);

        var now = DateTimeOffset.Parse("2026-08-07T15:00:00Z");
        var oldTime = now.AddDays(-100);
        var recentTime = now.AddDays(-1);

        var oldCommand = Command.Create(
            seed.Tenant.Id,
            device.Id,
            seed.User.Id,
            CommandAction.Open,
            oldTime.AddMinutes(30),
            CommandSource.MobileApp,
            "idem-old");
        var recentCommand = Command.Create(
            seed.Tenant.Id,
            device.Id,
            seed.User.Id,
            CommandAction.Close,
            recentTime.AddMinutes(30),
            CommandSource.MobileApp,
            "idem-new");

        var oldEvent = DeviceEvent.Create(
            seed.Tenant.Id,
            device.Id,
            "OPEN",
            EventResult.Success,
            EventOrigin.App,
            seed.User.Id);
        var recentEvent = DeviceEvent.Create(
            seed.Tenant.Id,
            device.Id,
            "CLOSE",
            EventResult.Success,
            EventOrigin.App,
            seed.User.Id);

        SetCreatedAt(oldCommand, oldTime);
        SetCreatedAt(recentCommand, recentTime);
        SetCreatedAt(oldEvent, oldTime);
        SetCreatedAt(recentEvent, recentTime);

        db.Commands.AddRange(oldCommand, recentCommand);
        db.DeviceEvents.AddRange(oldEvent, recentEvent);
        await db.SaveChangesAsync();

        var service = new HistoryRetentionService(
            db,
            new TestFixture.FakeClock(now),
            Options.Create(new HistoryRetentionOptions { RetentionDays = 90, BatchSize = 500 }),
            NullLogger<HistoryRetentionService>.Instance);

        var result = await service.PurgeExpiredAsync();

        result.CommandsDeleted.Should().Be(1);
        result.EventsDeleted.Should().Be(1);
        (await db.Commands.CountAsync()).Should().Be(1);
        (await db.DeviceEvents.CountAsync()).Should().Be(1);
        (await db.Commands.SingleAsync()).IdempotencyKey.Should().Be("idem-new");
    }

    private static void SetCreatedAt(Entity entity, DateTimeOffset createdAt)
    {
        typeof(Entity)
            .GetProperty(nameof(Entity.CreatedAt))!
            .SetValue(entity, createdAt);
    }
}

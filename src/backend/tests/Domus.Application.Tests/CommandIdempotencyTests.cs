using Domus.Application.Devices;
using Domus.Domain.Entities;
using Domus.Domain.Enums;
using Domus.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Domus.Application.Tests;

public class CommandIdempotencyTests
{
    [Fact]
    public async Task FindExisting_returns_command_with_same_device_and_key()
    {
        await using var db = CreateDb();
        var deviceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var first = Command.Create(tenantId, deviceId, Guid.NewGuid(), CommandAction.Open,
            DateTimeOffset.UtcNow.AddMinutes(1), idempotencyKey: "gate-open-1");
        var other = Command.Create(tenantId, deviceId, Guid.NewGuid(), CommandAction.Close,
            DateTimeOffset.UtcNow.AddMinutes(1), idempotencyKey: "gate-close-1");

        db.Commands.AddRange(first, other);
        await db.SaveChangesAsync();

        var service = new CommandIdempotencyService(db);
        var found = await service.FindExistingAsync(deviceId, "gate-open-1");

        found.Should().NotBeNull();
        found!.Id.Should().Be(first.Id);
    }

    [Fact]
    public async Task FindExisting_returns_null_for_unknown_key()
    {
        await using var db = CreateDb();
        var service = new CommandIdempotencyService(db);

        var found = await service.FindExistingAsync(Guid.NewGuid(), "missing");
        found.Should().BeNull();
    }

    private static DomusDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<DomusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DomusDbContext(options);
    }
}

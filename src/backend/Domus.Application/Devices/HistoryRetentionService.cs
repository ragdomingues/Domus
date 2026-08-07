using Domus.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Domus.Application.Devices;

public sealed record HistoryPurgeResult(
    DateTimeOffset CutoffUtc,
    int EventsDeleted,
    int CommandsDeleted);

public interface IHistoryRetentionService
{
    DateTimeOffset GetCutoffUtc();
    Task<HistoryPurgeResult> PurgeExpiredAsync(CancellationToken cancellationToken = default);
}

public sealed class HistoryRetentionService : IHistoryRetentionService
{
    private readonly IDomusDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly HistoryRetentionOptions _options;
    private readonly ILogger<HistoryRetentionService> _logger;

    public HistoryRetentionService(
        IDomusDbContext db,
        IDateTimeProvider clock,
        IOptions<HistoryRetentionOptions> options,
        ILogger<HistoryRetentionService> logger)
    {
        _db = db;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public DateTimeOffset GetCutoffUtc()
    {
        var days = Math.Clamp(_options.RetentionDays, 1, 3650);
        return _clock.UtcNow.AddDays(-days);
    }

    public async Task<HistoryPurgeResult> PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = GetCutoffUtc();
        var batchSize = Math.Clamp(_options.BatchSize, 100, 20_000);
        var eventsDeleted = 0;
        var commandsDeleted = 0;

        // Eventos primeiro (podem referenciar CommandId).
        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await _db.DeviceEvents
                .Where(e => e.CreatedAt < cutoff)
                .OrderBy(e => e.CreatedAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            _db.DeviceEvents.RemoveRange(batch);
            await _db.SaveChangesAsync(cancellationToken);
            eventsDeleted += batch.Count;

            if (batch.Count < batchSize)
            {
                break;
            }
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await _db.Commands
                .Where(c => c.CreatedAt < cutoff)
                .OrderBy(c => c.CreatedAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            _db.Commands.RemoveRange(batch);
            await _db.SaveChangesAsync(cancellationToken);
            commandsDeleted += batch.Count;

            if (batch.Count < batchSize)
            {
                break;
            }
        }

        if (eventsDeleted > 0 || commandsDeleted > 0)
        {
            _logger.LogInformation(
                "Retenção de histórico: removidos {Events} eventos e {Commands} comandos anteriores a {Cutoff:o}",
                eventsDeleted,
                commandsDeleted,
                cutoff);
        }

        return new HistoryPurgeResult(cutoff, eventsDeleted, commandsDeleted);
    }
}

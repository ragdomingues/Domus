using Domus.Application.Devices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Domus.Infrastructure.Messaging;

/// <summary>
/// Remove comandos e eventos com mais de N dias (padrão 90) para não crescer a base sem limite.
/// </summary>
public sealed class HistoryRetentionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<HistoryRetentionOptions> _options;
    private readonly ILogger<HistoryRetentionWorker> _logger;

    public HistoryRetentionWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<HistoryRetentionOptions> options,
        ILogger<HistoryRetentionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Aguarda a API estabilizar antes da primeira limpeza
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var retention = scope.ServiceProvider.GetRequiredService<IHistoryRetentionService>();
                var result = await retention.PurgeExpiredAsync(stoppingToken);
                _logger.LogDebug(
                    "Purge histórico concluído (eventos={Events}, comandos={Commands}, cutoff={Cutoff:o})",
                    result.EventsDeleted,
                    result.CommandsDeleted,
                    result.CutoffUtc);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Erro no worker de retenção de histórico");
            }

            var hours = Math.Clamp(_options.Value.IntervalHours, 1, 168);
            try
            {
                await Task.Delay(TimeSpan.FromHours(hours), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}

using Domus.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Domus.Infrastructure.Messaging;

public sealed class GateOpenAlertWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GateOpenAlertWorker> _logger;

    public GateOpenAlertWorker(IServiceScopeFactory scopeFactory, ILogger<GateOpenAlertWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var gateNotifications = scope.ServiceProvider.GetRequiredService<IGateNotificationService>();
                await gateNotifications.ProcessOpenTooLongAlertsAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Erro no worker de alerta de portão aberto");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}

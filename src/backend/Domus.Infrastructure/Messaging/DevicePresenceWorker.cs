using Domus.Application.Devices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Domus.Infrastructure.Messaging;

public sealed class DevicePresenceWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DevicePresenceWorker> _logger;

    public DevicePresenceWorker(IServiceScopeFactory scopeFactory, ILogger<DevicePresenceWorker> logger)
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
                var presence = scope.ServiceProvider.GetRequiredService<IDevicePresenceService>();
                await presence.MarkStaleDevicesOfflineAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Erro no worker de presença de dispositivos");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}

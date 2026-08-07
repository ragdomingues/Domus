using Domus.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Domus.Infrastructure.Messaging;

/// <summary>
/// Used when Mqtt:Enabled=false (local tests / Development without broker).
/// </summary>
public sealed class NullDeviceMessenger : IDeviceMessenger
{
    private readonly ILogger<NullDeviceMessenger> _logger;

    public NullDeviceMessenger(ILogger<NullDeviceMessenger> logger)
    {
        _logger = logger;
    }

    public Task PublishCommandAsync(
        Guid tenantId,
        Guid deviceId,
        Guid commandId,
        Guid correlationId,
        string action,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "IDeviceMessenger stub: command {CommandId} corr {CorrelationId} action {Action} device {DeviceId}",
            commandId, correlationId, action, deviceId);
        return Task.CompletedTask;
    }

    public Task PublishConfigurationAsync(
        Guid tenantId,
        Guid deviceId,
        string configurationJson,
        CancellationToken cancellationToken = default,
        bool retain = true)
    {
        _logger.LogInformation(
            "IDeviceMessenger stub: config for device {DeviceId} (retain={Retain})",
            deviceId,
            retain);
        return Task.CompletedTask;
    }
}

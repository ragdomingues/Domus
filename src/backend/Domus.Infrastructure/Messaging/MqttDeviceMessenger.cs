using System.Text.Json;
using System.Text.Json.Nodes;
using Domus.Application.Abstractions;
using Domus.Application.Devices;
using Microsoft.Extensions.Logging;
using MQTTnet.Protocol;

namespace Domus.Infrastructure.Messaging;

public sealed class MqttDeviceMessenger : IDeviceMessenger
{
    private readonly IMqttConnectionService _mqtt;
    private readonly ILogger<MqttDeviceMessenger> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public MqttDeviceMessenger(IMqttConnectionService mqtt, ILogger<MqttDeviceMessenger> logger)
    {
        _mqtt = mqtt;
        _logger = logger;
    }

    public async Task PublishCommandAsync(
        Guid tenantId,
        Guid deviceId,
        Guid commandId,
        Guid correlationId,
        string action,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            messageId = Guid.NewGuid(),
            commandId,
            correlationId,
            action,
            issuedAt,
            expiresAt
        }, JsonOptions);

        var topic = MqttTopics.Command(tenantId, deviceId);
        await _mqtt.PublishAsync(topic, payload, MqttQualityOfServiceLevel.AtLeastOnce, retain: false, cancellationToken);
        _logger.LogInformation("MQTT command published {CommandId} to {Topic}", commandId, topic);
    }

    public async Task PublishConfigurationAsync(
        Guid tenantId,
        Guid deviceId,
        string configurationJson,
        CancellationToken cancellationToken = default,
        bool retain = true)
    {
        var node = JsonNode.Parse(string.IsNullOrWhiteSpace(configurationJson) ? "{}" : configurationJson) as JsonObject
                   ?? new JsonObject();
        node["messageId"] = Guid.NewGuid().ToString();

        var topic = MqttTopics.Config(tenantId, deviceId);
        await _mqtt.PublishAsync(topic, node.ToJsonString(), MqttQualityOfServiceLevel.AtLeastOnce, retain, cancellationToken);
        _logger.LogInformation(
            "MQTT config published for device {DeviceId} (retain={Retain})",
            deviceId,
            retain);
    }
}

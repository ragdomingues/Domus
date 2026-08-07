using System.Text;
using Domus.Application.Devices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace Domus.Infrastructure.Messaging;

public interface IMqttConnectionService
{
    bool IsConnected { get; }
    Task PublishAsync(
        string topic,
        string payload,
        MqttQualityOfServiceLevel qos,
        bool retain,
        CancellationToken cancellationToken = default);
}

public sealed class MqttConnectionService : BackgroundService, IMqttConnectionService
{
    private readonly IMqttClient _client;
    private readonly MqttOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MqttConnectionService> _logger;
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    public MqttConnectionService(
        IOptions<MqttOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<MqttConnectionService> logger)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _client = new MqttFactory().CreateMqttClient();
        _client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
    }

    public bool IsConnected => _client.IsConnected;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("MQTT desabilitado (Mqtt:Enabled=false).");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnsureConnectedAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha no loop MQTT; reconectando em 5s.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    public async Task PublishAsync(
        string topic,
        string payload,
        MqttQualityOfServiceLevel qos,
        bool retain,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException("MQTT está desabilitado.");
        }

        await EnsureConnectedAsync(cancellationToken);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(qos)
            .WithRetainFlag(retain)
            .Build();

        await _client.PublishAsync(message, cancellationToken);
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_client.IsConnected)
        {
            return;
        }

        await _connectLock.WaitAsync(cancellationToken);
        try
        {
            if (_client.IsConnected)
            {
                return;
            }

            var builder = new MqttClientOptionsBuilder()
                .WithClientId($"{_options.ClientId}-{Environment.MachineName}")
                .WithCredentials(_options.Username, _options.Password)
                .WithCleanSession();

            builder = _options.UseTls
                ? builder.WithTcpServer(_options.Host, _options.Port).WithTlsOptions(o => o.UseTls())
                : builder.WithTcpServer(_options.Host, _options.Port);

            await _client.ConnectAsync(builder.Build(), cancellationToken);

            await _client.SubscribeAsync(
                new MqttTopicFilterBuilder().WithTopic(MqttTopics.StatusWildcard).WithAtLeastOnceQoS().Build(),
                cancellationToken);
            await _client.SubscribeAsync(
                new MqttTopicFilterBuilder().WithTopic(MqttTopics.HeartbeatWildcard).WithAtMostOnceQoS().Build(),
                cancellationToken);

            _logger.LogInformation("MQTT conectado a {Host}:{Port}", _options.Host, _options.Port);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var topic = args.ApplicationMessage.Topic;
        var payload = args.ApplicationMessage.ConvertPayloadToString() ?? string.Empty;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var telemetry = scope.ServiceProvider.GetRequiredService<IDeviceTelemetryService>();
            await telemetry.HandleIncomingAsync(topic, payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar mensagem MQTT {Topic}", topic);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_client.IsConnected)
        {
            await _client.DisconnectAsync(cancellationToken: cancellationToken);
        }

        await base.StopAsync(cancellationToken);
    }
}

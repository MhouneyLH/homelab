using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;

namespace HomelabBrain.DeviceConfig.Infrastructure;

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated via DI (AddHostedService)")]
internal sealed partial class DeviceConfigMqttConsumer : BackgroundService {
    private readonly IManagedMqttClient _client;
    private readonly MqttOptions _options;
    private readonly PendingCommandRegistry _registry;
    private readonly ILogger<DeviceConfigMqttConsumer> _logger;

    public DeviceConfigMqttConsumer(
        IManagedMqttClient client,
        IOptions<MqttOptions> options,
        PendingCommandRegistry registry,
        ILogger<DeviceConfigMqttConsumer> logger) {
        _client = client;
        _options = options.Value;
        _registry = registry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct) {
        _client.ApplicationMessageReceivedAsync += OnMessageReceived;

        MqttClientOptions clientOptions = new MqttClientOptionsBuilder()
            .WithClientId(_options.ClientId)
            .WithTcpServer(_options.BrokerHost, _options.BrokerPort)
            .Build();

        ManagedMqttClientOptions managedOptions = new ManagedMqttClientOptionsBuilder()
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
            .WithClientOptions(clientOptions)
            .Build();

        await _client.StartAsync(managedOptions).ConfigureAwait(false);
        await _client.SubscribeAsync("devices/+/config/#").ConfigureAwait(false);

        LogConsumerStarted(_options.BrokerHost, _options.BrokerPort);

        try {
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        } finally {
            await _client.StopAsync().ConfigureAwait(false);
        }
    }

    private Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e) {
        string topic = e.ApplicationMessage.Topic;
        if (!topic.EndsWith("/response", StringComparison.Ordinal))
            return Task.CompletedTask;

        string payload = e.ApplicationMessage.ConvertPayloadToString();

        JsonDocument doc;
        try {
            doc = JsonDocument.Parse(payload);
        } catch (JsonException) {
            LogInvalidResponsePayload(topic, payload);
            return Task.CompletedTask;
        }

        using (doc) {
            if (!doc.RootElement.TryGetProperty("correlationId", out JsonElement correlationIdElement)
                || correlationIdElement.ValueKind != JsonValueKind.String) {
                LogMissingCorrelationId(topic);
                return Task.CompletedTask;
            }

            string? correlationId = correlationIdElement.GetString();
            if (string.IsNullOrEmpty(correlationId)) {
                LogMissingCorrelationId(topic);
                return Task.CompletedTask;
            }

            _registry.Resolve(correlationId, doc.RootElement.Clone());
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Device config MQTT consumer started, subscribed to devices/+/config/# on {Host}:{Port}")]
    private partial void LogConsumerStarted(string host, int port);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid response payload on topic {Topic}: {Payload}")]
    private partial void LogInvalidResponsePayload(string topic, string payload);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Response on topic {Topic} missing correlationId")]
    private partial void LogMissingCorrelationId(string topic);
}

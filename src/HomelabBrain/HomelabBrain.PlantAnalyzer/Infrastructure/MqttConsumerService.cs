using System.Diagnostics.CodeAnalysis;
using HomelabBrain.PlantAnalyzer.Application;
using HomelabBrain.PlantAnalyzer.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;

namespace HomelabBrain.PlantAnalyzer.Infrastructure;

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated via DI (AddHostedService)")]
internal sealed partial class MqttConsumerService : BackgroundService {
    private readonly IManagedMqttClient _client;
    private readonly MqttOptions _options;
    private readonly PlantMetrics _metrics;
    private readonly ILogger<MqttConsumerService> _logger;

    public MqttConsumerService(
        IManagedMqttClient client,
        IOptions<MqttOptions> options,
        PlantMetrics metrics,
        ILogger<MqttConsumerService> logger) {
        _client = client;
        _options = options.Value;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct) {
        _client.ApplicationMessageReceivedAsync += OnMessageReceived;

        var clientOptions = new MqttClientOptionsBuilder()
            .WithClientId(_options.ClientId)
            .WithTcpServer(_options.BrokerHost, _options.BrokerPort)
            .Build();

        var managedOptions = new ManagedMqttClientOptionsBuilder()
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
            .WithClientOptions(clientOptions)
            .Build();

        await _client.StartAsync(managedOptions).ConfigureAwait(false);
        await _client.SubscribeAsync("plants/#").ConfigureAwait(false);

        LogConsumerStarted(_options.BrokerHost, _options.BrokerPort);

        try {
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
        } finally {
            await _client.StopAsync().ConfigureAwait(false);
        }
    }

    private Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e) {
        var topic = e.ApplicationMessage.Topic;
        var payload = e.ApplicationMessage.ConvertPayloadToString();

        _metrics.IncrementReceived();

        switch (MqttMessageParser.Parse(topic, payload)) {
            case MqttMessageResult.Valid valid:
                _metrics.RecordSensorReading(valid.Reading);
                LogSensorReading(valid.Reading.PlantId, valid.Reading.SensorType, valid.Reading.Value);
                break;

            case MqttMessageResult.InvalidPayload invalid:
                _metrics.IncrementInvalid("invalid_payload");
                LogInvalidPayload(invalid.Topic, invalid.RawPayload);
                break;

            case MqttMessageResult.UnknownTopic unknown:
                _metrics.IncrementInvalid("unknown_topic");
                LogUnknownTopic(unknown.Topic);
                break;

            default:
                break;
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "MQTT consumer started, subscribed to plants/# on {Host}:{Port}")]
    private partial void LogConsumerStarted(string host, int port);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sensor reading: plant={PlantId} sensor={SensorType} value={Value}")]
    private partial void LogSensorReading(string plantId, string sensorType, double value);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid payload on topic {Topic}: {Payload}")]
    private partial void LogInvalidPayload(string topic, string payload);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipped unknown topic: {Topic}")]
    private partial void LogUnknownTopic(string topic);
}

using HomelabBrain.PlantAnalyzer.Application;
using HomelabBrain.PlantAnalyzer.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;

namespace HomelabBrain.PlantAnalyzer.Infrastructure;

internal sealed class MqttConsumerService : BackgroundService
{
    private readonly IManagedMqttClient _client;
    private readonly MqttOptions _options;
    private readonly PlantMetrics _metrics;
    private readonly ILogger<MqttConsumerService> _logger;

    public MqttConsumerService(
        IManagedMqttClient client,
        IOptions<MqttOptions> options,
        PlantMetrics metrics,
        ILogger<MqttConsumerService> logger)
    {
        _client = client;
        _options = options.Value;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _client.ApplicationMessageReceivedAsync += OnMessageReceived;

        var clientOptions = new MqttClientOptionsBuilder()
            .WithClientId(_options.ClientId)
            .WithTcpServer(_options.BrokerHost, _options.BrokerPort)
            .Build();

        var managedOptions = new ManagedMqttClientOptionsBuilder()
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
            .WithClientOptions(clientOptions)
            .Build();

        await _client.StartAsync(managedOptions);
        await _client.SubscribeAsync("plants/#");

        _logger.LogInformation(
            "MQTT consumer started, subscribed to plants/# on {Host}:{Port}",
            _options.BrokerHost,
            _options.BrokerPort);

        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        finally
        {
            await _client.StopAsync();
        }
    }

    private Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic;
        var payload = e.ApplicationMessage.ConvertPayloadToString();

        _metrics.IncrementReceived();

        switch (MqttMessageParser.Parse(topic, payload))
        {
            case MqttMessageResult.Valid valid:
                _metrics.RecordSensorReading(valid.Reading);
                _logger.LogDebug(
                    "Sensor reading: plant={PlantId} sensor={SensorType} value={Value}",
                    valid.Reading.PlantId,
                    valid.Reading.SensorType,
                    valid.Reading.Value);
                break;

            case MqttMessageResult.InvalidPayload invalid:
                _metrics.IncrementInvalid("invalid_payload");
                _logger.LogWarning(
                    "Invalid payload on topic {Topic}: {Payload}",
                    invalid.Topic,
                    invalid.RawPayload);
                break;

            case MqttMessageResult.UnknownTopic unknown:
                _metrics.IncrementInvalid("unknown_topic");
                _logger.LogDebug("Skipped unknown topic: {Topic}", unknown.Topic);
                break;
        }

        return Task.CompletedTask;
    }
}

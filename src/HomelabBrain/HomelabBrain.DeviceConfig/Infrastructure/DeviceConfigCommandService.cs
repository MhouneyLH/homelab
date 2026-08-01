using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using HomelabBrain.DeviceConfig.Domain;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Extensions.ManagedClient;

namespace HomelabBrain.DeviceConfig.Infrastructure;

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated via DI (AddSingleton)")]
internal sealed class DeviceConfigCommandService {
    private readonly IManagedMqttClient _client;
    private readonly PendingCommandRegistry _registry;
    private readonly MqttOptions _options;

    public DeviceConfigCommandService(
        IManagedMqttClient client,
        PendingCommandRegistry registry,
        IOptions<MqttOptions> options) {
        _client = client;
        _registry = registry;
        _options = options.Value;
    }

    public async Task<ConfigCommandOutcome> SendAsync(
        DeviceId deviceId,
        string operation,
        IReadOnlyDictionary<string, object?> payload,
        CancellationToken ct) {
        string correlationId = Guid.NewGuid().ToString("N");

        Dictionary<string, object?> body = new(payload) { ["correlationId"] = correlationId };
        string json = JsonSerializer.Serialize(body);

        using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(_options.CommandTimeoutSeconds));
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        Task<JsonElement> responseTask = _registry.Register(correlationId, linkedCts.Token);

        MqttApplicationMessage message = new MqttApplicationMessageBuilder()
            .WithTopic($"devices/{deviceId.Value}/config/{operation}")
            .WithPayload(json)
            .Build();

        await _client.EnqueueAsync(message).ConfigureAwait(false);

        try {
            JsonElement response = await responseTask.ConfigureAwait(false);
            return new ConfigCommandOutcome.Success(response);
        } catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested) {
            return new ConfigCommandOutcome.TimedOut();
        }
    }
}

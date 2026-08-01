using System.Text.Json.Nodes;
using HomelabBrain.DeviceSimulator;
using Microsoft.Extensions.Configuration;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;

// Fixed so it's a stable, memorable target while developing - see AppHost/Program.cs
// for how this resource is wired up, and HomelabBrain.Api/HomelabBrain.Api.http for
// example requests against it.
const string DeviceId = "simulated-device-123";
const string PlantIdInitial = "plant-sim";

IConfigurationRoot configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

// Same Aspire endpoint-injection convention as PlantAnalyzerModule/DeviceConfigModule.
string brokerHost = "localhost";
int brokerPort = 1883;
string? aspireEndpoint = configuration["services:mosquitto:mqtt:0"];
if (aspireEndpoint is not null && Uri.TryCreate(aspireEndpoint, UriKind.Absolute, out Uri? uri)) {
    brokerHost = uri.Host;
    brokerPort = uri.Port;
}

SimulatedDevice device = new(DeviceId, "Simulated-WiFi", brokerHost, brokerPort, PlantIdInitial);

using IManagedMqttClient client = new MqttFactory().CreateManagedMqttClient();

client.ApplicationMessageReceivedAsync += async e => {
    string topic = e.ApplicationMessage.Topic;
    string prefix = device.ConfigTopicPrefix;
    if (!topic.StartsWith(prefix, StringComparison.Ordinal))
        return;

    string operation = topic[prefix.Length..];
    string payload = e.ApplicationMessage.ConvertPayloadToString();

    JsonObject? request;
    try {
        request = JsonNode.Parse(payload) as JsonObject;
    } catch (System.Text.Json.JsonException) {
        Console.WriteLine($"[simulator] Invalid JSON on {topic}: {payload}");
        return;
    }

    JsonObject? response = device.Handle(operation, request ?? []);
    if (response is null) {
        Console.WriteLine($"[simulator] Unknown operation: {operation}");
        return;
    }

    MqttApplicationMessage responseMessage = new MqttApplicationMessageBuilder()
        .WithTopic($"{topic}/response")
        .WithPayload(response.ToJsonString())
        .Build();
    await client.EnqueueAsync(responseMessage).ConfigureAwait(false);
};

MqttClientOptions clientOptions = new MqttClientOptionsBuilder()
    .WithClientId($"device-simulator-{DeviceId}")
    .WithTcpServer(brokerHost, brokerPort)
    .Build();

ManagedMqttClientOptions managedOptions = new ManagedMqttClientOptionsBuilder()
    .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
    .WithClientOptions(clientOptions)
    .Build();

await client.StartAsync(managedOptions).ConfigureAwait(false);
await client.SubscribeAsync($"{device.ConfigTopicPrefix}#").ConfigureAwait(false);

Console.WriteLine($"[simulator] Connected to {brokerHost}:{brokerPort} as device \"{DeviceId}\".");
Console.WriteLine($"[simulator] Try: GET/POST {{HostAddress}}/api/devices/{DeviceId}/config from HomelabBrain.Api.http");

using PeriodicTimer timer = new(TimeSpan.FromSeconds(5));
while (await timer.WaitForNextTickAsync().ConfigureAwait(false)) {
    string soilMoistureTopic = $"plants/{device.PlantId}/soil-moisture";
    MqttApplicationMessage reading = new MqttApplicationMessageBuilder()
        .WithTopic(soilMoistureTopic)
        .WithPayload(SimulatedDevice.BuildSoilMoistureReading())
        .Build();
    await client.EnqueueAsync(reading).ConfigureAwait(false);
}

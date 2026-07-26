using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// External broker: a real MQTT broker (see mosquitto/docker-compose.yml) reachable
// on the LAN, e.g. for the D1 mini soil moisture sensor. Falls back to appsettings'
// Mqtt:BrokerHost/BrokerPort (localhost:1883 in Development).
// Aspire-managed broker (default): Aspire spins up and wires the container itself,
// but its endpoint proxy binds to loopback only - not reachable from other devices.
bool useExternalMqttBroker = builder.Configuration.GetValue("UseExternalMqttBroker", false);

var api = builder.AddProject<Projects.HomelabBrain_Api>("api")
    .WithEnvironment("DOTNET_ENVIRONMENT", builder.Environment.EnvironmentName);

if (!useExternalMqttBroker) {
    var mosquitto = builder.AddContainer("mosquitto", "eclipse-mosquitto", "2.1.2")
        .WithEndpoint(port: 1883, targetPort: 1883, name: "mqtt")
        .WithEndpoint(port: 9001, targetPort: 9001, name: "websocket");

    api.WithReference(mosquitto.GetEndpoint("mqtt"))
        .WaitFor(mosquitto);
}

builder.Build().Run();

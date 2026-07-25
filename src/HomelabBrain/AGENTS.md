# HomelabBrain - Agent Context

## Architecture

Modulith monolith with vertical slices.
Each module is a separate project: `HomelabBrain.{ModuleName}`.
`HomelabBrain.Api` is the composition root - it references and wires all modules.

```
HomelabBrain.Api              - host, composition root
HomelabBrain.PlantAnalyzer    - module: MQTT consumer + plant sensor metrics
HomelabBrain.ServiceDefaults  - Aspire shared telemetry/health
HomelabBrain.AppHost          - Aspire orchestration (local dev only)
```

## Module Pattern

Every module exposes exactly two public methods on a `{Name}Module` static class:

```csharp
// registration
builder.AddPlantAnalyzer(this IHostApplicationBuilder builder)

// endpoint mapping
routes.MapPlantAnalyzer(this IEndpointRouteBuilder routes)
```

Called from `HomelabBrain.Api/Program.cs`.
All internal types stay `internal`.

## Vertical Slice Folder Structure

```
HomelabBrain.{Module}/
  Domain/          - records, value objects, union types
  Application/     - use cases, parsers, handlers
  Infrastructure/  - external I/O (MQTT, DB, HTTP), options, metrics
  {Module}Module.cs  - public AddX / MapX
```

## Union Types (Discriminated Unions)

Use sealed record hierarchies for all result/state types.
Abstract base = the union type.
Nested sealed records = cases.

```csharp
internal abstract record MqttMessageResult
{
    internal sealed record Valid(SensorReading Reading) : MqttMessageResult;
    internal sealed record InvalidPayload(string Topic, string RawPayload) : MqttMessageResult;
    internal sealed record UnknownTopic(string Topic) : MqttMessageResult;
}
```

Match with `switch` - compiler enforces exhaustiveness with sealed types.
Use unions for any operation that can produce different outcomes.

## MQTT Topic Convention

```
plants/{plantId}/{sensorType}
```

Payload: decimal number (float, invariant culture).
Examples: `plants/basil-1/moisture`, `plants/tomato-2/temperature`.

## Metrics

Meter name: `HomelabBrain.PlantAnalyzer`
Key metrics:
- `plants.sensor.reading` (histogram) - tags: `plant.id`, `sensor.type`
- `plants.mqtt.messages.received` (counter)
- `plants.mqtt.messages.invalid` (counter) - tag: `reason`

Metrics exported via OTLP (`OTEL_EXPORTER_OTLP_ENDPOINT`) to homelab OTel collector.
Each module self-registers its meter in `AddX()` via `AddOpenTelemetry().WithMetrics(m => m.AddMeter(...))`.

## Local Development

Run via Aspire AppHost:

```
dotnet run --project HomelabBrain.AppHost
```

Aspire starts: Mosquitto (1883/9001), OTel collector (4317/4318), API.
OTel collector config: `HomelabBrain.AppHost/otel-collector-config.yaml` (debug exporter by default - swap for real exporter targeting homelab collector).

Aspire injects Mosquitto endpoint as `services__mosquitto__mqtt__0=tcp://host:port`.
`PlantAnalyzerModule.AddPlantAnalyzer` reads this and overrides `Mqtt:BrokerHost`/`Mqtt:BrokerPort`.

## Production Config

Set in cluster secrets/configmap:
```
Mqtt__BrokerHost = <homelab mosquitto host>
Mqtt__BrokerPort = 1883
OTEL_EXPORTER_OTLP_ENDPOINT = <homelab otel collector grpc endpoint>
```

## Adding a New Module

1. Create `HomelabBrain.{Name}/` project (mirror PlantAnalyzer structure)
2. Add to `HomelabBrain.slnx`
3. Add `ProjectReference` in `HomelabBrain.Api/HomelabBrain.Api.csproj`
4. Call `builder.AddName()` and `app.MapName()` in `HomelabBrain.Api/Program.cs`
5. Register meter in `AddName()` via `AddOpenTelemetry().WithMetrics(m => m.AddMeter(...))`

## Package Management

All versions in `Directory.Packages.props` (central package management).
All build settings in `Directory.Build.props` (TFM: net11.0).
AppHost overrides TFM to net10.0 (Aspire 13.0 SDK constraint).

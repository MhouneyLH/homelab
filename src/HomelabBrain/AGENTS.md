# HomelabBrain - Agent Context

**Meta rule:** Always update this file when you learn something important for future sessions.

## Architecture

Modulith monolith with vertical slices.
Each module is a separate project: `HomelabBrain.{ModuleName}`.
`HomelabBrain.Api` is the composition root - it references and wires all modules.

```
HomelabBrain.Api              - host, composition root
HomelabBrain.PlantAnalyzer    - module: MQTT consumer + plant sensor metrics
HomelabBrain.DeviceConfig     - module: MQTT request-reply device config (WiFi/broker/plant-id)
HomelabBrain.DeviceSimulator  - fake device for local dev/testing, see "Device Simulator" below
HomelabBrain.ServiceDefaults  - Aspire shared telemetry/health
HomelabBrain.AppHost          - Aspire orchestration (local dev only)
```

- [`HomelabBrain.PlantAnalyzer`](./HomelabBrain.PlantAnalyzer)
- [`HomelabBrain.DeviceConfig`](./HomelabBrain.DeviceConfig)
- [`HomelabBrain.DeviceSimulator`](./HomelabBrain.DeviceSimulator)
- [`HomelabBrain.ServiceDefaults`](./HomelabBrain.ServiceDefaults)
- [`HomelabBrain.AppHost`](./HomelabBrain.AppHost)

## Module Pattern

Every module exposes exactly two public methods on a `{Name}Module` static class:

```csharp
builder.AddPlantAnalyzer(this IHostApplicationBuilder builder)
routes.MapPlantAnalyzer(this IEndpointRouteBuilder routes)
```

Called from [`HomelabBrain.Api/Program.cs`](./HomelabBrain.Api/Program.cs).
All internal types stay `internal`.

## Vertical Slice Folder Structure

```
HomelabBrain.{Module}/
  Domain/          - records, value objects, union types
  Application/     - use cases, parsers, handlers (message-driven modules, e.g. PlantAnalyzer)
  Endpoints/       - HTTP-triggered modules: one static class per endpoint, vertical-slice
                     style (nested Request/Response records, Handle, Validate all in one file)
  Infrastructure/  - external I/O (MQTT, DB, HTTP), options, metrics
  {Module}Module.cs  - public AddX / MapX
```

Use `Application/` for modules driven by an inbound message stream (MQTT consumer parsing/handling).
Use `Endpoints/` for modules exposing REST endpoints - see
[`HomelabBrain.DeviceConfig/Endpoints`](./HomelabBrain.DeviceConfig/Endpoints) for the pattern:
each file is self-contained (`Request`, `Response`, `Handle`, `Validate`), wired into routes from
`{Module}Module.MapX()`. Don't share validation/mapping logic across slices beyond small generic
infra (e.g.
[`ConfigCommandResults.ToApiResult`](./HomelabBrain.DeviceConfig/Endpoints/ConfigCommandResults.cs) -
HTTP status mapping, not business logic).

## Union Types (Discriminated Unions)

Use sealed record hierarchies for all result/state types.

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
Parsed in [`MqttMessageParser`](./HomelabBrain.PlantAnalyzer/Application/MqttMessageParser.cs).

## DeviceConfig MQTT Request-Reply Convention

Addressed by the device's stable chip-id (`DeviceId`), never by `plantId` - plantId is one of
the fields that can itself be changed by this API, so it can't double as the routing key.

```
devices/{deviceId}/config/wifi/set        {correlationId, ssid, password}
devices/{deviceId}/config/broker/set      {correlationId, host, port}
devices/{deviceId}/config/plant-id/set    {correlationId, plantId}
devices/{deviceId}/config/get             {correlationId}
```

Each publishes its result to `<topic>/response` with `{correlationId, status: "ok"|"error", ...}`.
`wifi/set` and `broker/set` reboot the device on success (new network/broker only takes effect after
reboot); `plant-id/set` applies live. The API
([`DeviceConfigCommandService`](./HomelabBrain.DeviceConfig/Infrastructure/DeviceConfigCommandService.cs))
generates the `correlationId`, publishes, and awaits the matching `/response` via
[`PendingCommandRegistry`](./HomelabBrain.DeviceConfig/Infrastructure/PendingCommandRegistry.cs)
(a `ConcurrentDictionary<correlationId, TaskCompletionSource>`), timing out after
`DeviceConfigMqtt:CommandTimeoutSeconds` (default 10s) -> HTTP 504.

Firmware side: [`src/hardware/gardening/src/ConfigCommands.cpp`](../hardware/gardening/src/ConfigCommands.cpp)
(see that project's [AGENTS.md](../hardware/gardening/AGENTS.md)/[README](../hardware/gardening/README.md)).

## Metrics

Meter name: `HomelabBrain.PlantAnalyzer`
([OpenTelemetry Metrics API](https://opentelemetry.io/docs/languages/net/instrumentation/#metrics))
Key metrics:
- `plants.sensor.reading` (histogram) - tags: `plant.id`, `sensor.type`
- `plants.mqtt.messages.received` (counter)
- `plants.mqtt.messages.invalid` (counter) - tag: `reason`

Defined in [`PlantMetrics.cs`](./HomelabBrain.PlantAnalyzer/Infrastructure/PlantMetrics.cs).
Metrics exported via OTLP (`OTEL_EXPORTER_OTLP_ENDPOINT`) to homelab
[OpenTelemetry Collector](https://opentelemetry.io/docs/collector/).
Each module self-registers its meter in `AddX()` via `AddOpenTelemetry().WithMetrics(m => m.AddMeter(...))`.

## Local Development

Run via [Aspire](https://learn.microsoft.com/dotnet/aspire/get-started/aspire-overview) AppHost:

```
dotnet restore  # generates packages.lock.json first time
dotnet run --project HomelabBrain.AppHost --launch-profile https
```

Or `--launch-profile http` for plain HTTP dashboard (needs `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true`,
already set in that profile in
[`HomelabBrain.AppHost/Properties/launchSettings.json`](./HomelabBrain.AppHost/Properties/launchSettings.json)).
Both profiles bring up the
[Aspire dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/overview)
with live metrics/traces for the API and [Mosquitto](https://mosquitto.org/).

Aspire starts: Mosquitto (1883/9001), OTel collector (4317/4318), API, DeviceSimulator (fixed
device id `simulated-device-123` - see
[`HomelabBrain.DeviceSimulator/AGENTS.md`](./HomelabBrain.DeviceSimulator/AGENTS.md)).
OTel collector config: `HomelabBrain.AppHost/otel-collector-config.yaml` (debug exporter - swap
for real endpoint in prod).

Aspire injects Mosquitto endpoint as `services__mosquitto__mqtt__0=tcp://host:port`.
[`PlantAnalyzerModule.AddPlantAnalyzer`](./HomelabBrain.PlantAnalyzer/PlantAnalyzerModule.cs) and
[`DeviceConfigModule.AddDeviceConfig`](./HomelabBrain.DeviceConfig/DeviceConfigModule.cs) both
read this and override `Mqtt:BrokerHost`/`Mqtt:BrokerPort` and
`DeviceConfigMqtt:BrokerHost`/`BrokerPort` respectively - each module owns its own
[MQTTnet](https://github.com/dotnet/MQTTnet) client/connection, deliberately not shared, so one
module's reconnect/subscribe churn can't affect the other. `MqttOptions.BrokerHost` defaults to
`"localhost"` in code (not `appsettings.json`) so the app - and build-time OpenAPI doc generation,
which boots the real host to introspect routes - never fails to start on missing config;
Aspire/production override it via the endpoint lookup above or a real config value.

### Device Simulator (testing DeviceConfig without real hardware)

[`HomelabBrain.DeviceSimulator`](./HomelabBrain.DeviceSimulator) starts automatically with AppHost
(Aspire-managed broker mode only) and answers MQTT config commands for a fixed device id
(`simulated-device-123`), so
[`HomelabBrain.Api.http`](./HomelabBrain.Api/HomelabBrain.Api.http) requests against that id work
end-to-end with no hardware flashed. Disable it with
`dotnet run --project HomelabBrain.AppHost -- IncludeDeviceSimulator=false` - e.g. to avoid two
"devices" both answering for the same id if you also want to test against real hardware sharing
the Aspire-managed broker. It's already off automatically under `UseExternalMqttBroker=true`.

### External Broker Mode (real devices on the LAN, e.g. the D1 mini soil sensor)

Aspire's container endpoints bind to loopback only, unreachable from other devices on the
network - by design, Aspire is a single-machine dev-loop tool, not meant to expose services
externally. For a broker real hardware needs to reach, run Mosquitto standalone instead:

Run from `src/HomelabBrain/`:

```
cd mosquitto && docker compose up -d
cd .. && dotnet run --project HomelabBrain.AppHost -- UseExternalMqttBroker=true
```

`UseExternalMqttBroker=true` skips the Aspire-managed Mosquitto container in
[`AppHost/Program.cs`](./HomelabBrain.AppHost/Program.cs). The API falls back to
`Mqtt:BrokerHost=localhost`
([`appsettings.Development.json`](./HomelabBrain.Api/appsettings.Development.json)), same port
(`1883`) the standalone broker publishes - hardware on the LAN connects to the PC's IP on that
port. Config:
[`HomelabBrain/mosquitto/docker-compose.yml`](./mosquitto/docker-compose.yml),
[`mosquitto.conf`](./mosquitto/mosquitto.conf); `mosquitto.conf` explicitly binds listeners to
`0.0.0.0` ([Docker](https://docs.docker.com/)'s own default bind is already all-interfaces, but
[Mosquitto](https://mosquitto.org/)'s own default `bind_address` inside the container is not, so
leaving it out reintroduces the loopback-only problem this mode exists to avoid).

Default (no flag) stays Aspire-managed - unaffected, still the right choice when no external
device needs to reach the broker.

## Production Config

Set in cluster secrets/configmap:
```
Mqtt__BrokerHost = <homelab mosquitto host>
Mqtt__BrokerPort = 1883
DeviceConfigMqtt__BrokerHost = <homelab mosquitto host>
DeviceConfigMqtt__BrokerPort = 1883
OTEL_EXPORTER_OTLP_ENDPOINT = <homelab otel collector grpc endpoint>
```

[Kubernetes probes](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/)
(`/healthz` liveness, `/readyz` readiness - implemented in
[`ServiceDefaultsExtensions.MapDefaultEndpoints`](./HomelabBrain.ServiceDefaults/Extensions.cs)):
```yaml
livenessProbe:
  httpGet: { path: /healthz, port: 8080 }
readinessProbe:
  httpGet: { path: /readyz, port: 8080 }
```

## OpenAPI / Scalar

[`HomelabBrain.Api.csproj`](./HomelabBrain.Api/HomelabBrain.Api.csproj) sets
`OpenApiGenerateDocumentsOnBuild=true`
([`Microsoft.Extensions.ApiDescription.Server`](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/aspnetcore-openapi)),
so [`HomelabBrain.Api/HomelabBrain.Api.json`](./HomelabBrain.Api/HomelabBrain.Api.json) (the
[OpenAPI](https://www.openapis.org/) contract) regenerates on every `dotnet build` and is
committed - it can never silently drift from the actual endpoints. Doc generation boots the real
host to introspect routes, which is why `MqttOptions.BrokerHost` needs a working default (see
Local Development above).

Interactive docs: `/scalar/v1` ([Scalar](https://github.com/scalar/scalar) UI). Raw spec:
`/openapi/v1.json`. Both `Development`-only, same as the existing `MapOpenApi()` gate in
[`Program.cs`](./HomelabBrain.Api/Program.cs).

## Code Style

Brace style: K&R (opening brace on same line as function head).
Enforced via [`.editorconfig`](./.editorconfig) ([EditorConfig](https://editorconfig.org/)).
Pre-commit hook runs `dotnet format --verify-no-changes` - format before committing.

To format: `dotnet format src/HomelabBrain/HomelabBrain.slnx`

`TreatWarningsAsErrors` + `AnalysisMode=AllEnabledByDefault` + `EnforceCodeStyleInBuild` (all in
[`Directory.Build.props`](./Directory.Build.props)) mean every analyzer/style diagnostic is a
build error - `dotnet build` is the real formatting/lint gate here, not just `dotnet format`. New
code must build clean under these settings; when a rule doesn't fit (e.g. `IDE0058` on fluent
builder chains), tune the rule in `.editorconfig` project-wide rather than suppressing per line,
and say why in the PR/commit.

## Git Hooks (Husky.Net)

Tool: [Husky.Net](https://alirezanet.github.io/Husky.Net/) (`.config/dotnet-tools.json` at repo
root). Hook config: [`.husky/task-runner.json`](../../.husky/task-runner.json).
Pre-commit: runs `dotnet format --verify-no-changes` on the solution.

Initial setup after cloning:
```
dotnet tool restore
dotnet husky install
```

## Package Management

All versions in [`Directory.Packages.props`](./Directory.Packages.props)
([central package management](https://learn.microsoft.com/nuget/consume-packages/central-package-management)).
`CentralPackageTransitivePinningEnabled=true` - transitive deps pinned in `Directory.Packages.props`.
After adding a new package, run `dotnet restore` and inspect `packages.lock.json` to discover transitive deps worth pinning.
All build settings in [`Directory.Build.props`](./Directory.Build.props) (TFM: net11.0).
AppHost overrides TFM to net10.0 (Aspire 13.0 SDK constraint).

## Lock Files

`RestorePackagesWithLockFile=true` + `RestoreLockedMode=true` both set in `Directory.Build.props`.
Lock files are always enforced - restore fails if `packages.lock.json` drifts from project deps.
Lock files must be committed.

When adding/updating packages, regenerate lock files first:
```
dotnet restore --property:RestoreLockedMode=false
```
Then commit the updated `packages.lock.json` files alongside the package change.

## Adding a New Module

1. Create `HomelabBrain.{Name}/` project (mirror
   [`HomelabBrain.PlantAnalyzer`](./HomelabBrain.PlantAnalyzer) structure)
2. Add to [`HomelabBrain.slnx`](./HomelabBrain.slnx)
3. Add `ProjectReference` in
   [`HomelabBrain.Api/HomelabBrain.Api.csproj`](./HomelabBrain.Api/HomelabBrain.Api.csproj)
4. Call `builder.AddName()` and `app.MapName()` in
   [`HomelabBrain.Api/Program.cs`](./HomelabBrain.Api/Program.cs)
5. Register meter in `AddName()` via `AddOpenTelemetry().WithMetrics(m => m.AddMeter(...))`
6. Add new package deps to `Directory.Packages.props` with proper `Label` on `ItemGroup`

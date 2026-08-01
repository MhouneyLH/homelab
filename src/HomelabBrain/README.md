# HomelabBrain

[.NET Aspire](https://learn.microsoft.com/dotnet/aspire/get-started/aspire-overview) modular
monolith that talks to homelab hardware over [MQTT](https://mqtt.org/) and exposes a REST API
(with [OpenAPI](https://www.openapis.org/)/[Scalar](https://github.com/scalar/scalar) docs) for
both reading plant sensor metrics and remotely reconfiguring devices in the field.

## Architecture

```
HomelabBrain.Api              - host, composition root (Program.cs wires every module)
HomelabBrain.PlantAnalyzer    - MQTT consumer: parses soil-moisture readings, records metrics
HomelabBrain.DeviceConfig     - MQTT request-reply: REST endpoints to reconfigure a device's
                                 WiFi/broker/plant-id, and to read its current config back
HomelabBrain.DeviceSimulator  - fake device (fixed id) for exercising DeviceConfig without
                                 real hardware - see "Testing" below
HomelabBrain.ServiceDefaults  - shared Aspire telemetry/health wiring
HomelabBrain.AppHost          - Aspire orchestration (local dev only)
```

- [`HomelabBrain.Api/Program.cs`](./HomelabBrain.Api/Program.cs)
- [`HomelabBrain.PlantAnalyzer`](./HomelabBrain.PlantAnalyzer)
- [`HomelabBrain.DeviceConfig`](./HomelabBrain.DeviceConfig)
- [`HomelabBrain.DeviceSimulator`](./HomelabBrain.DeviceSimulator)
- [`HomelabBrain.ServiceDefaults`](./HomelabBrain.ServiceDefaults)
- [`HomelabBrain.AppHost`](./HomelabBrain.AppHost)

Each module is a self-contained project exposing `AddX()` (DI wiring) and `MapX()` (routes), and
owns its own MQTT connection ([MQTTnet](https://github.com/dotnet/MQTTnet)) - PlantAnalyzer and
DeviceConfig never share a client, so one module's reconnect/subscribe churn can't affect the
other. See [`AGENTS.md`](./AGENTS.md) for the full internal conventions (folder layout, union
types, package/lock-file rules).

## MQTT Topics

Sensor readings (device -> API, fire-and-forget), nested under the device's stable chip-id, same
addressing principle as the config topics below:
```
devices/{deviceId}/plants/{plantId}/{sensorType}     e.g. devices/a1b2c3d4/plants/basil-1/soil-moisture
```
Payload carries the **raw ADC reading** (`rawValue`, 0-1023), not a calibrated percentage -
[`HomelabBrain.PlantAnalyzer`](./HomelabBrain.PlantAnalyzer) converts it via
[`SoilMoistureCalibrator`](./HomelabBrain.PlantAnalyzer/Application/SoilMoistureCalibrator.cs)
and `SoilMoistureCalibration:DryRaw`/`WetRaw`, so recalibrating a drifting sensor is a config
change, not a reflash.

Device config, request-reply (API -> device -> API), addressed the same way:
```
devices/{deviceId}/config/wifi/set        -> reboots device on success
devices/{deviceId}/config/broker/set      -> reboots device on success
devices/{deviceId}/config/plant-id/set    -> applied live, no reboot
devices/{deviceId}/config/get             -> current config (WiFi password never echoed back)
```
Every command gets a response on `<topic>/response`, correlated by a `correlationId` the API
generates per request and awaits with a timeout
([`DeviceConfigCommandService`](./HomelabBrain.DeviceConfig/Infrastructure/DeviceConfigCommandService.cs),
`DeviceConfigMqtt:CommandTimeoutSeconds`, default 10s -> HTTP 504 if the device never answers).
See the firmware side in [`src/hardware/gardening`](../hardware/gardening), particularly
[`ConfigCommands.cpp`](../hardware/gardening/src/ConfigCommands.cpp).

## REST API

`POST /api/devices/{deviceId}/config/wifi`, `/broker`, `/plant-id` and
`GET /api/devices/{deviceId}/config` - see the live docs below for full request/response shapes,
or browse the endpoint implementations directly:
[`SetWifiEndpoint.cs`](./HomelabBrain.DeviceConfig/Endpoints/SetWifiEndpoint.cs),
[`SetBrokerEndpoint.cs`](./HomelabBrain.DeviceConfig/Endpoints/SetBrokerEndpoint.cs),
[`SetPlantIdEndpoint.cs`](./HomelabBrain.DeviceConfig/Endpoints/SetPlantIdEndpoint.cs),
[`GetConfigEndpoint.cs`](./HomelabBrain.DeviceConfig/Endpoints/GetConfigEndpoint.cs).

- Interactive docs (Development only): `/scalar/v1`
  ([Scalar](https://github.com/scalar/scalar) API reference UI)
- Raw OpenAPI spec: `/openapi/v1.json`, also committed at
  [`HomelabBrain.Api/HomelabBrain.Api.json`](./HomelabBrain.Api/HomelabBrain.Api.json) - regenerated
  automatically on every build via
  [`Microsoft.Extensions.ApiDescription.Server`](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/aspnetcore-openapi),
  so it can't drift from the real endpoints.
- Example requests: [`HomelabBrain.Api.http`](./HomelabBrain.Api/HomelabBrain.Api.http) - see
  "Getting Started" below for editor setup.

## Health Endpoints

`/healthz` (liveness) and `/readyz` (readiness) - available in every environment, not just
Development, since a
[Kubernetes liveness/readiness probe](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/)
needs them wherever the pod actually runs. Implemented in
[`ServiceDefaultsExtensions.MapDefaultEndpoints`](./HomelabBrain.ServiceDefaults/Extensions.cs).
`/healthz` is a shallow "is the process alive" check only; `/readyz` runs every registered health
check, including dependencies (so a down MQTT broker takes the pod out of rotation without
restarting it - restarting wouldn't fix an external dependency anyway).

## Getting Started

Prerequisites: [.NET SDK](https://dotnet.microsoft.com/download) matching
[`Directory.Build.props`](../HomelabBrain/Directory.Build.props) (currently a `net11.0` preview
SDK; the AppHost project targets `net10.0` per the
[Aspire SDK](https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling)'s own
constraint).

```
dotnet tool restore     # installs Husky.Net from .config/dotnet-tools.json
dotnet husky install    # one-time: points git's core.hooksPath at .husky/
dotnet restore src/HomelabBrain/HomelabBrain.slnx
```

### Git hooks (Husky.Net)

[`dotnet husky install`](https://alirezanet.github.io/Husky.Net/guide/) wires
[`.husky/pre-commit`](../../.husky/pre-commit) into git, which runs
`dotnet husky run --group pre-commit` on every commit. That currently runs one task (see
[`.husky/task-runner.json`](../../.husky/task-runner.json)):
`dotnet format src/HomelabBrain/HomelabBrain.slnx --verify-no-changes`, which fails the commit if
any staged `.cs` file isn't already formatted per
[`.editorconfig`](https://editorconfig.org/). Run `dotnet format src/HomelabBrain/HomelabBrain.slnx`
yourself first if it blocks you. Without the `dotnet husky install` step above, the hook script
exists on disk but git never invokes it - that's a one-time setup step per clone, not something
that happens automatically.

### Running it

```
dotnet run --project HomelabBrain.AppHost --launch-profile http
```

Brings up the [Aspire dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/overview)
with the API, [Mosquitto](https://mosquitto.org/), and the
[OpenTelemetry Collector](https://opentelemetry.io/docs/collector/), all wired together. For
hardware on the LAN (not just this machine) to reach the broker, see "External Broker Mode" in
[`AGENTS.md`](./AGENTS.md).

### Testing DeviceConfig without real hardware

The AppHost run above also starts [`HomelabBrain.DeviceSimulator`](./HomelabBrain.DeviceSimulator)
by default - a fake device with a fixed id (`simulated-device-123`) that answers the same MQTT
config-command contract a real gardening device would (see
[its AGENTS.md](./HomelabBrain.DeviceSimulator/AGENTS.md)), minus flash persistence and an actual
reboot. Point [`HomelabBrain.Api.http`](./HomelabBrain.Api/HomelabBrain.Api.http) at
`deviceId = simulated-device-123` and every endpoint (set-wifi, set-broker, set-plant-id,
get-config) works end-to-end without flashing anything. It also publishes a fake soil-moisture
reading every 5s, so `HomelabBrain.PlantAnalyzer`'s metrics have something to record too. Set
`IncludeDeviceSimulator=false` (or `UseExternalMqttBroker=true`, which already implies it - see
[`AppHost/Program.cs`](./HomelabBrain.AppHost/Program.cs)) to turn it off, e.g. when testing
against real hardware sharing the same broker.

To edit/run [`HomelabBrain.Api.http`](./HomelabBrain.Api/HomelabBrain.Api.http) in VS Code,
install the [httpYac extension](https://marketplace.visualstudio.com/items?itemName=anweber.vscode-httpyac) -
see the comment at the top of that file for why (not the more popular "REST Client" extension).

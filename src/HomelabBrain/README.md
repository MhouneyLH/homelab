# HomelabBrain

.NET Aspire modular monolith that talks to homelab hardware over MQTT and exposes a REST API
(with OpenAPI/Scalar docs) for both reading plant sensor metrics and remotely reconfiguring
devices in the field.

## Architecture

```
HomelabBrain.Api              - host, composition root (Program.cs wires every module)
HomelabBrain.PlantAnalyzer    - MQTT consumer: parses soil-moisture readings, records metrics
HomelabBrain.DeviceConfig     - MQTT request-reply: REST endpoints to reconfigure a device's
                                 WiFi/broker/plant-id, and to read its current config back
HomelabBrain.ServiceDefaults  - shared Aspire telemetry/health wiring
HomelabBrain.AppHost          - Aspire orchestration (local dev only)
```

Each module is a self-contained project exposing `AddX()` (DI wiring) and `MapX()` (routes), and
owns its own MQTT connection - PlantAnalyzer and DeviceConfig never share a client, so one
module's reconnect/subscribe churn can't affect the other. See `AGENTS.md` for the full internal
conventions (folder layout, union types, package/lock-file rules).

## MQTT Topics

Sensor readings (device -> API, fire-and-forget):
```
plants/{plantId}/{sensorType}          e.g. plants/basil-1/soil-moisture
```

Device config, request-reply (API -> device -> API), addressed by the device's stable chip-id
rather than `plantId` (since plantId is itself one of the reconfigurable fields):
```
devices/{deviceId}/config/wifi/set        -> reboots device on success
devices/{deviceId}/config/broker/set      -> reboots device on success
devices/{deviceId}/config/plant-id/set    -> applied live, no reboot
devices/{deviceId}/config/get             -> current config (WiFi password never echoed back)
```
Every command gets a response on `<topic>/response`, correlated by a `correlationId` the API
generates per request and awaits with a timeout (`DeviceConfigMqtt:CommandTimeoutSeconds`,
default 10s -> HTTP 504 if the device never answers). See the firmware side in
[`src/hardware/gardening`](../hardware/gardening).

## REST API

`POST /api/devices/{deviceId}/config/wifi`, `/broker`, `/plant-id` and
`GET /api/devices/{deviceId}/config` - see the live docs below for full request/response shapes.

- Interactive docs (Development only): `/scalar/v1`
- Raw OpenAPI spec: `/openapi/v1.json`, also committed at
  [`HomelabBrain.Api/HomelabBrain.Api.json`](./HomelabBrain.Api/HomelabBrain.Api.json) - regenerated
  automatically on every build, so it can't drift from the real endpoints.

## Getting Started

Prerequisites: .NET SDK matching `Directory.Build.props` (currently a `net11.0` preview SDK; the
AppHost project targets `net10.0` per the Aspire SDK's own constraint).

```
dotnet tool restore     # installs Husky.Net from .config/dotnet-tools.json
dotnet husky install    # one-time: points git's core.hooksPath at .husky/
dotnet restore src/HomelabBrain/HomelabBrain.slnx
```

### Git hooks (Husky.Net)

`dotnet husky install` wires `.husky/pre-commit` into git, which runs
`dotnet husky run --group pre-commit` on every commit. That currently runs one task (see
`.husky/task-runner.json`): `dotnet format src/HomelabBrain/HomelabBrain.slnx --verify-no-changes`,
which fails the commit if any staged `.cs` file isn't already formatted per `.editorconfig`. Run
`dotnet format src/HomelabBrain/HomelabBrain.slnx` yourself first if it blocks you. Without the
`dotnet husky install` step above, the hook script exists on disk but git never invokes it -
that's a one-time setup step per clone, not something that happens automatically.

### Running it

```
dotnet run --project HomelabBrain.AppHost --launch-profile https
```

Brings up the Aspire dashboard with the API, Mosquitto, and OTel collector, all wired together.
For hardware on the LAN (not just this machine) to reach the broker, see "External Broker Mode"
in `AGENTS.md`.

# HomelabBrain.DeviceSimulator - Agent Context

Fake gardening device for local dev - see [`../../hardware/gardening`](../../hardware/gardening)
for the real firmware this mirrors, and [`../AGENTS.md`](../AGENTS.md)'s "Device Simulator"
section for how it's wired into AppHost.

## What It Is

A console app, not an ASP.NET Core module - it doesn't follow the `AddX()`/`MapX()` module
pattern used elsewhere in this solution, since it has no DI container or HTTP surface of its own,
just an [MQTTnet](https://github.com/dotnet/MQTTnet) client. Started by
[`HomelabBrain.AppHost`](../HomelabBrain.AppHost) alongside the API and
[Mosquitto](https://mosquitto.org/).

## Contract Parity With Firmware

[`SimulatedDevice.cs`](./SimulatedDevice.cs) re-implements
[`ConfigCommands.cpp`](../../hardware/gardening/src/ConfigCommands.cpp)'s four operations
(`wifi/set`, `broker/set`, `plant-id/set`, `get`) with the same validation rules, same JSON
shapes, same topic convention (`devices/{deviceId}/config/...`). If you change one side, change
the other - there's no shared code between the C++ firmware and this C# simulator (different
languages, different toolchains), so parity is manual and easy to let drift. Grep both files side
by side before trusting the simulator to represent real device behavior.

Differences from a real device (by design, not oversights):
- No flash persistence - state resets on restart.
- `wifi/set`/`broker/set` don't actually reboot (can't meaningfully simulate that) - they just
  log what a real device would do and ack immediately.
- Fixed device id (`simulated-device-123`), not derived from a chip id (there's no chip).

## Fixed Device Id

`simulated-device-123`, defined as a `const` in [`Program.cs`](./Program.cs). Keep it in sync
with [`HomelabBrain.Api.http`](../HomelabBrain.Api/HomelabBrain.Api.http)'s example requests and
this project's own log output on startup if you ever change it.

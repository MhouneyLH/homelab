# Gardening Firmware

D1 mini firmware publishing soil moisture readings over MQTT.

## Sensor

Capacitive Soil Moisture Sensor v2.0
(https://techtonions.com/products/capacitive-soil-moisture-sensor-v2.0).

- 3 pins: `VCC`, `GND`, `AOUT` (analog output).
- Operating voltage: 3.3V-5.5V. Wire `VCC` to D1 mini `3V3` (analog input tops out
  at 3.3V after the onboard divider - do not feed it 5V directly).
- `AOUT` -> D1 mini `A0`.
- Capacitive design: no corrosion over time, unlike resistive soil sensors.
- Output is inverted relative to raw moisture: **higher voltage/ADC value = drier
  soil, lower value = wetter soil**.
- Typical raw `analogRead()` range on this board is roughly 1023 (dry, sensor in
  air) down to ~300-400 (fully submerged in water) - exact numbers vary per unit
  and must be calibrated:
  1. Read raw value with sensor dry (in air) -> record as `DRY_RAW`.
  2. Read raw value with sensor in water -> record as `WET_RAW`.
  3. Map `analogRead()` between those two points, inverted, to get 0-100%:
     `percent = map(raw, DRY_RAW, WET_RAW, 0, 100)`.

`readSoilMoisturePercent()` in `src/main.cpp` currently uses placeholder
`map(raw, 0, 1023, 0, 100)` (not inverted, not calibrated) - replace with real
`DRY_RAW`/`WET_RAW` constants once measured.

## D1 Mini Pinout

Reference: https://lastminuteengineers.com/wemos-d1-mini-pinout-reference/

| Label | GPIO    | Function                                    | Safe for general use |
|-------|---------|----------------------------------------------|-----------------------|
| D0    | GPIO16  | HIGH at boot, deep-sleep wake                | Caution               |
| D1    | GPIO5   | I2C SCL (default)                            | Safe                  |
| D2    | GPIO4   | I2C SDA (default)                            | Safe                  |
| D3    | GPIO0   | Tied to FLASH button, boot fails if pulled LOW | Avoid               |
| D4    | GPIO2   | HIGH at boot, boot fails if pulled LOW       | Caution               |
| D5    | GPIO14  | SPI CLK                                      | Safe                  |
| D6    | GPIO12  | SPI MISO                                     | Safe                  |
| D7    | GPIO13  | SPI MOSI                                     | Safe                  |
| D8    | GPIO15  | Required for boot, boot fails if pulled HIGH | Avoid                 |
| RX    | GPIO3   | UART Rx, used for flashing/debugging         | Avoid                 |
| TX    | GPIO1   | UART Tx, used for flashing/debugging         | Avoid                 |
| A0    | -       | ADC, 10-bit, 0-3.3V range (only analog pin)  | Safe                  |
| 3V3   | -       | 3.3V regulator output, up to 600mA           | -                     |
| 5V    | -       | USB power output                             | -                     |
| GND   | -       | Ground                                       | -                     |
| RST   | -       | Reset                                        | -                     |

Soil moisture sensor `AOUT` wired to `A0` (only analog input on this board).

## Configuration

WiFi/MQTT/plant-id defaults are injected at compile time from `.env`
(`INIT_WIFI_SSID`, `INIT_WIFI_PASSWORD`, `INIT_MQTT_BROKER_HOST`,
`INIT_MQTT_BROKER_PORT`, `INIT_PLANT_ID`), loaded by `load_env.py`
(a PlatformIO `pre:` extra script) directly into `CPPDEFINES` - no shell
involved, so special characters in the WiFi password are safe. Copy
`.env.example` to `.env`, fill in real values, then just build:

```
pio run
```

`.env` is gitignored; never commit real credentials. These are only the
device's *initial* config - once flashed, WiFi/broker/plant-id can be
changed at runtime, see "Runtime reconfiguration" below.

## Architecture

```
src/main.cpp              - setup()/loop(), WiFi/NTP/MQTT connect, sensor publish
src/DeviceConfig.cpp       - DeviceConfig struct, LittleFS load/save (/config.json)
src/ConfigCommands.cpp     - MQTT config command handlers (set-wifi/broker/plant-id, get)
include/generated_config.h - gitignored, written by load_env.py from .env at build time
```

On boot, `loadDeviceConfig()` reads `/config.json` from LittleFS; on first boot (no file yet) it
seeds from the compile-time defaults in `generated_config.h` and persists them. From then on,
flash contents - not the `.env` the firmware was built with - are the source of truth, so
reflashing doesn't silently revert a config change made at runtime.

## Runtime Reconfiguration

The device subscribes to MQTT config commands under `devices/{chip-id}/config/` (chip-id from
`ESP.getChipId()`, stable across reconfigs - unlike `plantId`, which is itself one of the fields
that can change). See [`HomelabBrain README`](../../HomelabBrain/README.md#mqtt-topics) for the
full topic list and the REST API (`HomelabBrain.DeviceConfig`) that drives this from the other
end. In short:

- `wifi/set`, `broker/set`: persisted to flash, acknowledged over the *current* connection, then
  `ESP.restart()` - the new network/broker only takes effect after reboot.
- `plant-id/set`: persisted and applied immediately (just changes the soil-moisture publish
  topic), no reboot needed.
- `get`: returns the current config (WiFi password never included in the response).

## Serial Upload Permissions

See `AGENTS.md`.

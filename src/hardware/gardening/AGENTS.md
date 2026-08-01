# AGENTS.md

## Build-Time Config Generation

[`include/generated_config.h`](./include/generated_config.h) is gitignored and written by
[`load_env.py`](./load_env.py)
([PlatformIO `pre:` extra script](https://docs.platformio.org/en/latest/scripting/actions.html))
from `.env` on every `pio run`. Never hand-edit it or commit it - if it's missing, `pio run`
regenerates it from `.env` (see [README's Configuration section](./README.md#configuration)).
Secrets are written there, not passed as `-D` build flags, because
[SCons](https://scons.org/)/argv word-splits on spaces even without a shell - a WiFi
password/SSID with a space or `&` silently corrupts the build otherwise (learned the hard way;
see git history on `load_env.py`).

`MQTT_MAX_PACKET_SIZE` is bumped to 512 in [`platformio.ini`](./platformio.ini) (`build_flags`) -
[PubSubClient](https://pubsubclient.knolleary.net/)'s default 256-byte buffer is too small for
the config command JSON payloads ([`ConfigCommands.cpp`](./src/ConfigCommands.cpp)). If you add
fields to a command payload and things start silently truncating, check this first.

## Serial Upload Permissions

Uploading via `esptool.py` / PlatformIO to `/dev/ttyUSB0` fails with:

```
PermissionError: [Errno 13] Permission denied: '/dev/ttyUSB0'
```

Cause: device owned by group `dialout`, user not member.

Fix (persistent, requires logout/login to take effect):

```
sudo usermod -aG dialout $USER
```

Quick workaround (resets on unplug/reboot, no relog needed):

```
sudo chmod 666 /dev/ttyUSB0
```

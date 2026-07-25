# AGENTS.md

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

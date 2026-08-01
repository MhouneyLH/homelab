#pragma once

#include <Arduino.h>

struct DeviceConfig {
  String wifiSsid;
  String wifiPassword;
  String mqttBrokerHost;
  uint16_t mqttBrokerPort;
  String plantId;
};

// Stable per-device identifier (hex chip ID), used to route MQTT config
// commands regardless of the (mutable) plantId.
String deviceId();

// Loads persisted config from flash, seeding it from the compile-time
// generated_config.h defaults on first boot.
DeviceConfig loadDeviceConfig();

void saveDeviceConfig(const DeviceConfig &config);

// plantId becomes an MQTT topic segment ("plants/{plantId}/soil-moisture"),
// so MQTT-special characters (/, +, #) and whitespace are rejected. Mirrors
// HomelabBrain.DeviceConfig's SetPlantIdEndpoint pattern (^[a-zA-Z0-9-]{1,32}$) -
// this is the last line of defense against a device publishing straight to
// the broker and bypassing the API's own validation, so it must match.
bool isValidPlantId(const String &plantId);

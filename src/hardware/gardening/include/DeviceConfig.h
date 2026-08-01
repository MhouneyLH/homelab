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

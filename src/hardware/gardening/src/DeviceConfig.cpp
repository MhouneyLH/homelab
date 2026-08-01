#include "DeviceConfig.h"
#include "generated_config.h"

#include <ArduinoJson.h>
#include <LittleFS.h>

namespace {

const char *const CONFIG_PATH = "/config.json";

DeviceConfig defaultConfig() {
  return DeviceConfig{WIFI_SSID, WIFI_PASSWORD, MQTT_BROKER_HOST, MQTT_BROKER_PORT, PLANT_ID};
}

} // namespace

String deviceId() {
  char buf[9];
  snprintf(buf, sizeof(buf), "%08x", ESP.getChipId());
  return String(buf);
}

DeviceConfig loadDeviceConfig() {
  if (!LittleFS.begin()) {
    Serial.println("LittleFS mount failed, formatting...");
    LittleFS.format();
    LittleFS.begin();
  }

  if (!LittleFS.exists(CONFIG_PATH)) {
    DeviceConfig config = defaultConfig();
    saveDeviceConfig(config);
    return config;
  }

  File file = LittleFS.open(CONFIG_PATH, "r");
  JsonDocument doc;
  DeserializationError err = deserializeJson(doc, file);
  file.close();

  if (err) {
    Serial.printf("Config parse failed (%s), using defaults.\n", err.c_str());
    return defaultConfig();
  }

  DeviceConfig config;
  config.wifiSsid = doc["wifiSsid"] | WIFI_SSID;
  config.wifiPassword = doc["wifiPassword"] | WIFI_PASSWORD;
  config.mqttBrokerHost = doc["mqttBrokerHost"] | MQTT_BROKER_HOST;
  config.mqttBrokerPort = doc["mqttBrokerPort"] | MQTT_BROKER_PORT;
  config.plantId = doc["plantId"] | PLANT_ID;
  return config;
}

void saveDeviceConfig(const DeviceConfig &config) {
  JsonDocument doc;
  doc["wifiSsid"] = config.wifiSsid;
  doc["wifiPassword"] = config.wifiPassword;
  doc["mqttBrokerHost"] = config.mqttBrokerHost;
  doc["mqttBrokerPort"] = config.mqttBrokerPort;
  doc["plantId"] = config.plantId;

  File file = LittleFS.open(CONFIG_PATH, "w");
  serializeJson(doc, file);
  file.close();
}

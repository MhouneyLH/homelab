#include "ConfigCommands.h"
#include "DeviceConfig.h"

#include <ArduinoJson.h>
#include <PubSubClient.h>

extern PubSubClient mqttClient;
extern DeviceConfig config;
extern String soilMoistureTopic;

namespace {

String configPrefix() {
  return "devices/" + deviceId() + "/config/";
}

void publishResponse(const String &topic, JsonDocument &response) {
  char buf[320];
  serializeJson(response, buf);
  mqttClient.publish((topic + "/response").c_str(), buf);
}

void rejectInvalidPayload(const String &topic, JsonDocument &response) {
  response["status"] = "error";
  response["error"] = "invalid payload";
  publishResponse(topic, response);
}

void handleWifiSet(const String &topic, JsonDocument &request, JsonDocument &response) {
  if (!request["ssid"].is<const char *>() || !request["password"].is<const char *>()) {
    rejectInvalidPayload(topic, response);
    return;
  }

  config.wifiSsid = request["ssid"].as<String>();
  config.wifiPassword = request["password"].as<String>();
  saveDeviceConfig(config);

  response["status"] = "ok";
  publishResponse(topic, response);

  Serial.println("WiFi config updated, rebooting...");
  delay(200);
  ESP.restart();
}

void handleBrokerSet(const String &topic, JsonDocument &request, JsonDocument &response) {
  if (!request["host"].is<const char *>() || !request["port"].is<int>()) {
    rejectInvalidPayload(topic, response);
    return;
  }

  config.mqttBrokerHost = request["host"].as<String>();
  config.mqttBrokerPort = request["port"].as<uint16_t>();
  saveDeviceConfig(config);

  response["status"] = "ok";
  publishResponse(topic, response);

  Serial.println("Broker config updated, rebooting...");
  delay(200);
  ESP.restart();
}

void handlePlantIdSet(const String &topic, JsonDocument &request, JsonDocument &response) {
  if (!request["plantId"].is<const char *>()) {
    rejectInvalidPayload(topic, response);
    return;
  }

  config.plantId = request["plantId"].as<String>();
  saveDeviceConfig(config);
  soilMoistureTopic = "plants/" + config.plantId + "/soil-moisture";

  response["status"] = "ok";
  publishResponse(topic, response);
}

void handleGet(const String &topic, JsonDocument &response) {
  response["status"] = "ok";
  response["wifiSsid"] = config.wifiSsid;
  response["mqttBrokerHost"] = config.mqttBrokerHost;
  response["mqttBrokerPort"] = config.mqttBrokerPort;
  response["plantId"] = config.plantId;
  publishResponse(topic, response);
}

} // namespace

void subscribeConfigCommands() {
  String prefix = configPrefix();
  mqttClient.subscribe((prefix + "wifi/set").c_str());
  mqttClient.subscribe((prefix + "broker/set").c_str());
  mqttClient.subscribe((prefix + "plant-id/set").c_str());
  mqttClient.subscribe((prefix + "get").c_str());
}

bool handleConfigCommand(const String &topic, const String &payload) {
  String prefix = configPrefix();
  if (!topic.startsWith(prefix))
    return false;

  String operation = topic.substring(prefix.length());

  JsonDocument request;
  DeserializationError parseError = deserializeJson(request, payload);

  JsonDocument response;
  response["correlationId"] = parseError ? "" : (request["correlationId"] | "");

  if (parseError) {
    rejectInvalidPayload(topic, response);
    return true;
  }

  if (operation == "wifi/set") {
    handleWifiSet(topic, request, response);
  } else if (operation == "broker/set") {
    handleBrokerSet(topic, request, response);
  } else if (operation == "plant-id/set") {
    handlePlantIdSet(topic, request, response);
  } else if (operation == "get") {
    handleGet(topic, response);
  } else {
    return false;
  }

  return true;
}

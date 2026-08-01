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

// plantId becomes an MQTT topic segment ("plants/{plantId}/soil-moisture"),
// so MQTT-special characters (/, +, #) and whitespace are rejected. Mirrors
// HomelabBrain.DeviceConfig's SetPlantIdEndpoint pattern (^[a-zA-Z0-9-]{1,32}$) -
// this is the last line of defense against a device publishing straight to
// the broker and bypassing the API's own validation, so it must match.
bool isValidPlantId(const String &plantId) {
  if (plantId.length() < 1 || plantId.length() > 32)
    return false;

  for (unsigned int i = 0; i < plantId.length(); i++) {
    char c = plantId[i];
    bool isAlphaNumeric = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
    if (!isAlphaNumeric && c != '-')
      return false;
  }

  return true;
}

void handleWifiSet(const String &topic, JsonDocument &request, JsonDocument &response) {
  if (!request["ssid"].is<const char *>() || !request["password"].is<const char *>()) {
    rejectInvalidPayload(topic, response);
    return;
  }

  String ssid = request["ssid"].as<String>();
  String password = request["password"].as<String>();

  bool ssidValid = ssid.length() >= 1 && ssid.length() <= 32;
  bool passwordValid = password.length() == 0 || (password.length() >= 8 && password.length() <= 63);
  if (!ssidValid || !passwordValid) {
    rejectInvalidPayload(topic, response);
    return;
  }

  config.wifiSsid = ssid;
  config.wifiPassword = password;
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

  String host = request["host"].as<String>();
  int port = request["port"].as<int>();
  if (host.length() < 1 || port < 1 || port > 65535) {
    rejectInvalidPayload(topic, response);
    return;
  }

  config.mqttBrokerHost = host;
  config.mqttBrokerPort = (uint16_t)port;
  saveDeviceConfig(config);

  response["status"] = "ok";
  publishResponse(topic, response);

  Serial.println("Broker config updated, rebooting...");
  delay(200);
  ESP.restart();
}

void handlePlantIdSet(const String &topic, JsonDocument &request, JsonDocument &response) {
  if (!request["plantId"].is<const char *>() || !isValidPlantId(request["plantId"].as<String>())) {
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

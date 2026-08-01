#include <Arduino.h>
#include <ESP8266WiFi.h>
#include <PubSubClient.h>
#include <ArduinoJson.h>
#include <time.h>
#include "ConfigCommands.h"
#include "DeviceConfig.h"
#include "generated_config.h"

#ifndef WIFI_SSID
#error "WIFI_SSID not defined - set INIT_WIFI_SSID in .env (see .env.example)"
#endif
#ifndef WIFI_PASSWORD
#error "WIFI_PASSWORD not defined - set INIT_WIFI_PASSWORD in .env (see .env.example)"
#endif
#ifndef MQTT_BROKER_HOST
#error "MQTT_BROKER_HOST not defined - set INIT_MQTT_BROKER_HOST in .env (see .env.example)"
#endif
#ifndef MQTT_BROKER_PORT
#error "MQTT_BROKER_PORT not defined - set INIT_MQTT_BROKER_PORT in .env (see .env.example)"
#endif
#ifndef PLANT_ID
#error "PLANT_ID not defined - set INIT_PLANT_ID in .env (see .env.example)"
#endif

static const unsigned long PUBLISH_INTERVAL_MS = 1000;
static const int SOIL_MOISTURE_PIN = A0;

WiFiClient wifiClient;
PubSubClient mqttClient(wifiClient);

DeviceConfig config;
String soilMoistureTopic;
String mqttClientId;

unsigned long lastPublish = 0;

void connectWiFi()
{
  Serial.printf("Connecting to WiFi SSID \"%s\"...\n", config.wifiSsid.c_str());
  WiFi.mode(WIFI_STA);
  WiFi.begin(config.wifiSsid.c_str(), config.wifiPassword.c_str());
  while (WiFi.status() != WL_CONNECTED)
  {
    delay(500);
    Serial.print(".");
  }
  Serial.printf("\nWiFi connected, IP: %s\n", WiFi.localIP().toString().c_str());
}

void syncTime()
{
  Serial.println("Syncing time via NTP...");
  configTime(0, 0, "pool.ntp.org", "time.nist.gov");
  while (time(nullptr) < 1'000'000'000)
  {
    delay(500);
    Serial.print(".");
  }
  Serial.println("\nTime synced.");
}

void connectMqtt()
{
  Serial.printf("Connecting to MQTT broker %s:%d...\n", config.mqttBrokerHost.c_str(), config.mqttBrokerPort);
  while (!mqttClient.connected())
  {
    if (mqttClient.connect(mqttClientId.c_str()))
    {
      Serial.println("MQTT connected.");
      subscribeConfigCommands();
    }
    else
    {
      Serial.printf("MQTT connect failed, rc=%d, retrying...\n", mqttClient.state());
      delay(1000);
    }
  }
}

void onMqttMessage(char *topic, byte *payload, unsigned int length)
{
  String payloadStr;
  payloadStr.reserve(length);
  for (unsigned int i = 0; i < length; i++)
  {
    payloadStr += (char)payload[i];
  }

  handleConfigCommand(String(topic), payloadStr);
}

double readSoilMoisturePercent()
{
  int raw = analogRead(SOIL_MOISTURE_PIN);
  return map(raw, 0, 1023, 0, 100);
}

String isoTimestampNow()
{
  time_t now = time(nullptr);
  struct tm utc;
  gmtime_r(&now, &utc);
  char buf[sizeof("2024-01-01T00:00:00Z")];
  strftime(buf, sizeof(buf), "%Y-%m-%dT%H:%M:%SZ", &utc);
  return String(buf);
}

void publishReading()
{
  JsonDocument doc;
  doc["valueInPercent"] = readSoilMoisturePercent();
  doc["measuredAt"] = isoTimestampNow();

  char payload[128];
  serializeJson(doc, payload);

  mqttClient.publish(soilMoistureTopic.c_str(), payload);
  Serial.printf("Published to %s: %s\n", soilMoistureTopic.c_str(), payload);
}

void setup()
{
  Serial.begin(115200);

  config = loadDeviceConfig();
  soilMoistureTopic = "plants/" + config.plantId + "/soil-moisture";
  mqttClientId = "d1-mini-gardening-" + deviceId();

  connectWiFi();
  syncTime();
  mqttClient.setServer(config.mqttBrokerHost.c_str(), config.mqttBrokerPort);
  mqttClient.setCallback(onMqttMessage);
  connectMqtt();
}

void loop()
{
  if (!mqttClient.connected())
  {
    connectMqtt();
  }
  mqttClient.loop();

  unsigned long now = millis();
  if (now - lastPublish >= PUBLISH_INTERVAL_MS)
  {
    lastPublish = now;
    publishReading();
  }
}

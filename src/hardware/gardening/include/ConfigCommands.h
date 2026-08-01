#pragma once

#include <Arduino.h>

// Request-reply MQTT config commands, addressed by the device's stable
// chip-id (not plantId, since plantId itself can be one of the mutated
// fields). Subscribes on (re)connect; call from the PubSubClient callback
// for every received message.
//
// Topics (under "devices/{deviceId}/config/"):
//   wifi/set      {correlationId, ssid, password}       -> reboots on success
//   broker/set    {correlationId, host, port}            -> reboots on success
//   plant-id/set  {correlationId, plantId}                -> applied live
//   get           {correlationId}                         -> current config (no password)
// Each publishes its result to "<topic>/response".

void subscribeConfigCommands();

// Returns true if the topic was a recognized config command (handled or
// rejected with an error response); false if the caller should ignore it.
bool handleConfigCommand(const String &topic, const String &payload);

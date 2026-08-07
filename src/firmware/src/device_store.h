#pragma once

#include <Arduino.h>
#include "domus_config.h"

struct DeviceCredentials {
  char deviceId[40]{};
  char tenantId[40]{};
  char mqttUsername[64]{};
  char mqttPassword[128]{};
  char topicCommand[160]{};
  char topicStatus[160]{};
  char topicHeartbeat[160]{};
  char topicConfig[160]{};
  bool valid = false;
};

struct RuntimeConfig {
  uint16_t relayPulseMs = DEFAULT_RELAY_PULSE_MS;
  uint16_t heartbeatIntervalSeconds = DEFAULT_HEARTBEAT_SEC;
  uint16_t commandTimeoutSeconds = DEFAULT_COMMAND_TIMEOUT_SEC;
  bool supportsClose = true;
  bool supportsStop = false;
};

struct WifiConfig {
  char ssid[33]{};
  char password[65]{};
  bool valid = false;
};

struct NetworkConfig {
  char apiBaseUrl[128]{};
  char mqttHost[64]{};
  uint16_t mqttPort = 1883;
  char provisioningCode[64]{};
  bool valid = false;
};

bool loadCredentials(DeviceCredentials& out);
bool saveCredentials(const DeviceCredentials& creds);
void clearCredentials();

bool loadRuntimeConfig(RuntimeConfig& out);
bool saveRuntimeConfig(const RuntimeConfig& cfg);

bool loadWifiConfig(WifiConfig& out);
bool saveWifiConfig(const WifiConfig& cfg);
void clearWifiConfig();

bool loadNetworkConfig(NetworkConfig& out);
bool saveNetworkConfig(const NetworkConfig& cfg);
void clearNetworkConfig();

void clearPendingProvisioningCode();

bool loadLastOtaUrl(char* dest, size_t len);
bool saveLastOtaUrl(const char* url);

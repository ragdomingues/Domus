#include "device_store.h"
#include <Preferences.h>

namespace {
Preferences prefs;

constexpr const char* NS_CREDS = "domus";
constexpr const char* NS_NET = "domusnet";

void copyPref(Preferences& p, const char* key, char* dest, size_t len) {
  String v = p.getString(key, "");
  strncpy(dest, v.c_str(), len - 1);
  dest[len - 1] = '\0';
}
}  // namespace

bool loadCredentials(DeviceCredentials& out) {
  out = DeviceCredentials{};
  if (!prefs.begin(NS_CREDS, true)) {
    return false;
  }
  out.valid = prefs.getBool("ok", false);
  if (!out.valid) {
    prefs.end();
    return false;
  }
  copyPref(prefs, "deviceId", out.deviceId, sizeof(out.deviceId));
  copyPref(prefs, "tenantId", out.tenantId, sizeof(out.tenantId));
  copyPref(prefs, "user", out.mqttUsername, sizeof(out.mqttUsername));
  copyPref(prefs, "pass", out.mqttPassword, sizeof(out.mqttPassword));
  copyPref(prefs, "tCmd", out.topicCommand, sizeof(out.topicCommand));
  copyPref(prefs, "tSt", out.topicStatus, sizeof(out.topicStatus));
  copyPref(prefs, "tHb", out.topicHeartbeat, sizeof(out.topicHeartbeat));
  copyPref(prefs, "tCfg", out.topicConfig, sizeof(out.topicConfig));
  prefs.end();
  return out.mqttUsername[0] != '\0' && out.mqttPassword[0] != '\0';
}

bool saveCredentials(const DeviceCredentials& creds) {
  if (!prefs.begin(NS_CREDS, false)) {
    return false;
  }
  prefs.putString("deviceId", creds.deviceId);
  prefs.putString("tenantId", creds.tenantId);
  prefs.putString("user", creds.mqttUsername);
  prefs.putString("pass", creds.mqttPassword);
  prefs.putString("tCmd", creds.topicCommand);
  prefs.putString("tSt", creds.topicStatus);
  prefs.putString("tHb", creds.topicHeartbeat);
  prefs.putString("tCfg", creds.topicConfig);
  prefs.putBool("ok", true);
  prefs.end();
  return true;
}

void clearCredentials() {
  if (prefs.begin(NS_CREDS, false)) {
    prefs.clear();
    prefs.end();
  }
}

bool loadRuntimeConfig(RuntimeConfig& out) {
  out = RuntimeConfig{};
  if (!prefs.begin(NS_CREDS, true)) {
    return false;
  }
  out.relayPulseMs = static_cast<uint16_t>(prefs.getUShort("pulse", DEFAULT_RELAY_PULSE_MS));
  out.heartbeatIntervalSeconds =
      static_cast<uint16_t>(prefs.getUShort("hb", DEFAULT_HEARTBEAT_SEC));
  out.commandTimeoutSeconds =
      static_cast<uint16_t>(prefs.getUShort("cto", DEFAULT_COMMAND_TIMEOUT_SEC));
  out.supportsClose = prefs.getBool("supClose", true);
  out.supportsStop = prefs.getBool("supStop", false);
  prefs.end();
  return true;
}

bool saveRuntimeConfig(const RuntimeConfig& cfg) {
  if (!prefs.begin(NS_CREDS, false)) {
    return false;
  }
  prefs.putUShort("pulse", cfg.relayPulseMs);
  prefs.putUShort("hb", cfg.heartbeatIntervalSeconds);
  prefs.putUShort("cto", cfg.commandTimeoutSeconds);
  prefs.putBool("supClose", cfg.supportsClose);
  prefs.putBool("supStop", cfg.supportsStop);
  prefs.end();
  return true;
}

bool loadWifiConfig(WifiConfig& out) {
  out = WifiConfig{};
  if (!prefs.begin(NS_NET, true)) {
    return false;
  }
  out.valid = prefs.getBool("wifiOk", false);
  if (!out.valid) {
    prefs.end();
    return false;
  }
  copyPref(prefs, "ssid", out.ssid, sizeof(out.ssid));
  copyPref(prefs, "wpass", out.password, sizeof(out.password));
  prefs.end();
  out.valid = out.ssid[0] != '\0';
  return out.valid;
}

bool saveWifiConfig(const WifiConfig& cfg) {
  if (!prefs.begin(NS_NET, false)) {
    return false;
  }
  prefs.putString("ssid", cfg.ssid);
  prefs.putString("wpass", cfg.password);
  prefs.putBool("wifiOk", cfg.ssid[0] != '\0');
  prefs.end();
  return true;
}

void clearWifiConfig() {
  if (!prefs.begin(NS_NET, false)) {
    return;
  }
  prefs.remove("ssid");
  prefs.remove("wpass");
  prefs.putBool("wifiOk", false);
  prefs.end();
}

bool loadNetworkConfig(NetworkConfig& out) {
  out = NetworkConfig{};
  if (!prefs.begin(NS_NET, true)) {
    return false;
  }
  out.valid = prefs.getBool("netOk", false);
  if (!out.valid) {
    prefs.end();
    return false;
  }
  copyPref(prefs, "api", out.apiBaseUrl, sizeof(out.apiBaseUrl));
  copyPref(prefs, "mqtt", out.mqttHost, sizeof(out.mqttHost));
  out.mqttPort = static_cast<uint16_t>(prefs.getUShort("mport", 1883));
  copyPref(prefs, "pcode", out.provisioningCode, sizeof(out.provisioningCode));
  prefs.end();
  out.valid = out.apiBaseUrl[0] != '\0' && out.mqttHost[0] != '\0';
  return out.valid;
}

bool saveNetworkConfig(const NetworkConfig& cfg) {
  if (!prefs.begin(NS_NET, false)) {
    return false;
  }
  prefs.putString("api", cfg.apiBaseUrl);
  prefs.putString("mqtt", cfg.mqttHost);
  prefs.putUShort("mport", cfg.mqttPort == 0 ? 1883 : cfg.mqttPort);
  prefs.putString("pcode", cfg.provisioningCode);
  prefs.putBool("netOk", cfg.apiBaseUrl[0] != '\0' && cfg.mqttHost[0] != '\0');
  prefs.end();
  return true;
}

void clearNetworkConfig() {
  if (prefs.begin(NS_NET, false)) {
    prefs.clear();
    prefs.end();
  }
}

void clearPendingProvisioningCode() {
  if (!prefs.begin(NS_NET, false)) {
    return;
  }
  prefs.putString("pcode", "");
  prefs.end();
}

bool loadLastOtaUrl(char* dest, size_t len) {
  if (!dest || len == 0) {
    return false;
  }
  dest[0] = '\0';
  if (!prefs.begin(NS_NET, true)) {
    return false;
  }
  copyPref(prefs, "otaUrl", dest, len);
  prefs.end();
  return dest[0] != '\0';
}

bool saveLastOtaUrl(const char* url) {
  if (!prefs.begin(NS_NET, false)) {
    return false;
  }
  prefs.putString("otaUrl", url ? url : "");
  prefs.end();
  return true;
}

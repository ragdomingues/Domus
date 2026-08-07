#include "provisioning.h"
#include <HTTPClient.h>
#include <WiFi.h>
#include <ArduinoJson.h>
#include "domus_config.h"

namespace {
String chipHardwareId() {
  uint64_t mac = ESP.getEfuseMac();
  char buf[32];
  snprintf(buf, sizeof(buf), "esp32-%04X%08X", static_cast<uint16_t>(mac >> 32), static_cast<uint32_t>(mac));
  return String(buf);
}
}  // namespace

bool activateDevice(const char* apiBaseUrl, const char* provisioningCode, DeviceCredentials& out) {
  out = DeviceCredentials{};
  if (!apiBaseUrl || !provisioningCode || WiFi.status() != WL_CONNECTED) {
    return false;
  }

  String url = String(apiBaseUrl);
  if (url.endsWith("/")) {
    url.remove(url.length() - 1);
  }
  url += "/api/devices/activate";

  JsonDocument body;
  body["provisioningCode"] = provisioningCode;
  body["hardwareId"] = chipHardwareId();
  body["firmwareVersion"] = FIRMWARE_VERSION;

  String payload;
  serializeJson(body, payload);

  HTTPClient http;
  http.setTimeout(15000);
  if (!http.begin(url)) {
    Serial.println("[prov] http.begin failed");
    return false;
  }
  http.addHeader("Content-Type", "application/json");
  http.addHeader("Accept", "application/json");

  const int code = http.POST(payload);
  const String response = http.getString();
  http.end();

  Serial.printf("[prov] HTTP %d\n", code);
  if (code < 200 || code >= 300) {
    Serial.println(response);
    return false;
  }

  JsonDocument doc;
  if (deserializeJson(doc, response)) {
    Serial.println("[prov] JSON inválido");
    return false;
  }

  // ASP.NET camelCase
  const char* deviceId = doc["deviceId"] | "";
  const char* tenantId = doc["tenantId"] | "";
  const char* user = doc["mqttUsername"] | "";
  const char* pass = doc["mqttPassword"] | "";
  const char* tCmd = doc["topicCommand"] | "";
  const char* tSt = doc["topicStatus"] | "";
  const char* tHb = doc["topicHeartbeat"] | "";
  const char* tCfg = doc["topicConfig"] | "";

  if (!*user || !*pass || !*tCmd) {
    Serial.println("[prov] resposta sem credenciais");
    return false;
  }

  strncpy(out.deviceId, deviceId, sizeof(out.deviceId) - 1);
  strncpy(out.tenantId, tenantId, sizeof(out.tenantId) - 1);
  strncpy(out.mqttUsername, user, sizeof(out.mqttUsername) - 1);
  strncpy(out.mqttPassword, pass, sizeof(out.mqttPassword) - 1);
  strncpy(out.topicCommand, tCmd, sizeof(out.topicCommand) - 1);
  strncpy(out.topicStatus, tSt, sizeof(out.topicStatus) - 1);
  strncpy(out.topicHeartbeat, tHb, sizeof(out.topicHeartbeat) - 1);
  strncpy(out.topicConfig, tCfg, sizeof(out.topicConfig) - 1);
  out.valid = true;

  if (!saveCredentials(out)) {
    Serial.println("[prov] falha ao salvar NVS");
    return false;
  }

  Serial.println("[prov] ativado — credenciais MQTT persistidas");
  return true;
}

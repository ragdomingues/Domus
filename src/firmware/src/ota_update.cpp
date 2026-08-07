#include "ota_update.h"

#include <HTTPUpdate.h>
#include <WiFi.h>
#include <WiFiClient.h>
#include <WiFiClientSecure.h>
#include <esp_task_wdt.h>

#include "device_store.h"
#include "domus_config.h"
#include "mqtt_ca_certs.h"

bool performOtaUpdate(const char* url) {
  if (!url || !*url) {
    return false;
  }
  if (WiFi.status() != WL_CONNECTED) {
    Serial.println("[ota] Wi-Fi offline");
    return false;
  }

  Serial.printf("[ota] iniciando %s\n", url);
  esp_task_wdt_reset();

  HTTPUpdate httpUpdate;
  httpUpdate.rebootOnUpdate(true);
  httpUpdate.onProgress([](int cur, int total) {
    static uint32_t last = 0;
    const uint32_t now = millis();
    if (now - last > 1000) {
      last = now;
      Serial.printf("[ota] %d / %d\n", cur, total);
      esp_task_wdt_reset();
    }
  });

  t_httpUpdate_return ret;
  if (strncmp(url, "https://", 8) == 0) {
    WiFiClientSecure client;
#if defined(DOMUS_MQTT_TLS_INSECURE)
    client.setInsecure();
#elif defined(MQTT_CA_CERT)
    client.setCACert(MQTT_CA_CERT);
#else
    client.setCACert(DOMUS_DEFAULT_MQTT_CA);
#endif
    ret = httpUpdate.update(client, url);
  } else {
    WiFiClient client;
    ret = httpUpdate.update(client, url);
  }

  switch (ret) {
    case HTTP_UPDATE_FAILED:
      Serial.printf("[ota] falha: %s\n", httpUpdate.getLastErrorString().c_str());
      return false;
    case HTTP_UPDATE_NO_UPDATES:
      Serial.println("[ota] sem atualização");
      return false;
    case HTTP_UPDATE_OK:
      saveLastOtaUrl(url);
      Serial.println("[ota] ok — reiniciando");
      return true;
    default:
      return false;
  }
}

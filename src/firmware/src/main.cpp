#include <Arduino.h>
#include <WiFi.h>
#include <esp_task_wdt.h>

#include "domus_config.h"

#if __has_include("secrets.h")
#include "secrets.h"
#else
#warning "include/secrets.h ausente — use SoftAP ou copie secrets.h.example"
#define WIFI_SSID ""
#define WIFI_PASSWORD ""
#define DOMUS_API_BASE_URL "http://192.168.1.10:8080"
#define MQTT_HOST "192.168.1.10"
#define MQTT_PORT 1883
#define PROVISIONING_CODE ""
#endif

#ifndef MQTT_PORT
#define MQTT_PORT 1883
#endif

#include "device_store.h"
#include "domus_mqtt.h"
#include "limit_switch.h"
#include "provisioning.h"
#include "relay_driver.h"
#include "wifi_portal.h"

namespace {
RelayDriver relay;
LimitSwitch limits;
DomusMqtt mqtt;
DeviceCredentials creds;
RuntimeConfig runtimeCfg;
WifiConfig wifiCfg;
NetworkConfig netCfg;
bool wifiReady = false;

void resolveNetworkDefaults() {
  if (!loadWifiConfig(wifiCfg) || !wifiCfg.valid) {
    if (WIFI_SSID[0] != '\0') {
      strncpy(wifiCfg.ssid, WIFI_SSID, sizeof(wifiCfg.ssid) - 1);
      strncpy(wifiCfg.password, WIFI_PASSWORD, sizeof(wifiCfg.password) - 1);
      wifiCfg.valid = true;
    }
  }

  if (!loadNetworkConfig(netCfg) || !netCfg.valid) {
    strncpy(netCfg.apiBaseUrl, DOMUS_API_BASE_URL, sizeof(netCfg.apiBaseUrl) - 1);
    strncpy(netCfg.mqttHost, MQTT_HOST, sizeof(netCfg.mqttHost) - 1);
    netCfg.mqttPort = static_cast<uint16_t>(MQTT_PORT);
    netCfg.valid = netCfg.apiBaseUrl[0] != '\0' && netCfg.mqttHost[0] != '\0';
  }

  if (netCfg.provisioningCode[0] == '\0' && PROVISIONING_CODE[0] != '\0') {
    strncpy(netCfg.provisioningCode, PROVISIONING_CODE, sizeof(netCfg.provisioningCode) - 1);
  }
}

bool connectWifi(const WifiConfig& wifi) {
  if (!wifi.valid || wifi.ssid[0] == '\0') {
    return false;
  }

  Serial.printf("[wifi] connecting to %s\n", wifi.ssid);
  WiFi.mode(WIFI_STA);
  WiFi.begin(wifi.ssid, wifi.password);

  const uint32_t start = millis();
  while (WiFi.status() != WL_CONNECTED && millis() - start < WIFI_CONNECT_TIMEOUT_MS) {
    delay(250);
    Serial.print('.');
    esp_task_wdt_reset();
  }
  Serial.println();

  if (WiFi.status() != WL_CONNECTED) {
    Serial.println("[wifi] falha");
    return false;
  }

  Serial.print("[wifi] IP ");
  Serial.println(WiFi.localIP());
  return true;
}

void syncTime() {
  setenv("TZ", "UTC0", 1);
  tzset();
  configTime(0, 0, "pool.ntp.org", "time.google.com");
  Serial.print("[ntp] sync");
  for (int i = 0; i < 40; i++) {
    if (time(nullptr) > 100000) {
      Serial.println(" ok");
      return;
    }
    delay(250);
    Serial.print('.');
    esp_task_wdt_reset();
  }
  Serial.println(" timeout (continua sem relógio)");
}

bool ensureProvisioned() {
  if (loadCredentials(creds) && creds.valid) {
    Serial.printf("[boot] credenciais NVS user=%s\n", creds.mqttUsername);
    return true;
  }

  const char* code = nullptr;
  if (netCfg.provisioningCode[0] != '\0') {
    code = netCfg.provisioningCode;
  } else if (PROVISIONING_CODE[0] != '\0') {
    code = PROVISIONING_CODE;
  }

  Serial.println("[boot] sem credenciais — tentando activate");
  if (!code || !*code) {
    Serial.println("[boot] código de ativação ausente (portal SoftAP ou secrets.h)");
    return false;
  }

  if (!activateDevice(netCfg.apiBaseUrl, code, creds)) {
    return false;
  }

  clearPendingProvisioningCode();
  return loadCredentials(creds);
}

void handleSerialLine(const String& line) {
  if (line.startsWith("erase-creds")) {
    clearCredentials();
    Serial.println("[nvs] credenciais MQTT apagadas — reinicie para re-provisionar");
  } else if (line.startsWith("erase-wifi")) {
    clearWifiConfig();
    Serial.println("[nvs] Wi-Fi apagado — reinicie (ou use portal)");
  } else if (line.startsWith("erase-all")) {
    clearCredentials();
    clearNetworkConfig();
    Serial.println("[nvs] tudo apagado — reinicie para SoftAP");
  } else if (line.startsWith("setup")) {
    Serial.println("[setup] abrindo portal…");
    runSetupPortal(&wifiCfg, &netCfg);
  }
}
}  // namespace

void setup() {
  Serial.begin(115200);
  delay(200);
  Serial.println();
  Serial.printf("Domus ESP32 firmware %s (Rossi gate)\n", FIRMWARE_VERSION);

#if defined(ESP_ARDUINO_VERSION_MAJOR) && (ESP_ARDUINO_VERSION_MAJOR >= 3)
  {
    esp_task_wdt_config_t wdt = {
        .timeout_ms = WATCHDOG_TIMEOUT_SEC * 1000,
        .idle_core_mask = 0,
        .trigger_panic = true,
    };
    esp_task_wdt_reconfigure(&wdt);
  }
#else
  esp_task_wdt_init(WATCHDOG_TIMEOUT_SEC, true);
#endif
  esp_task_wdt_add(nullptr);

  relay.begin();
  limits.begin();
  loadRuntimeConfig(runtimeCfg);
  resolveNetworkDefaults();

  const bool forcePortal = setupButtonHeldAtBoot();
  if (forcePortal || !wifiCfg.valid) {
    runSetupPortal(wifiCfg.valid ? &wifiCfg : nullptr, netCfg.valid ? &netCfg : nullptr);
    return;
  }

  wifiReady = connectWifi(wifiCfg);
  if (!wifiReady) {
    Serial.println("[boot] Wi-Fi falhou — abrindo portal SoftAP");
    runSetupPortal(&wifiCfg, &netCfg);
    return;
  }

  syncTime();

  if (!ensureProvisioned()) {
    Serial.println("[boot] provisioning falhou — abrindo portal para novo código");
    runSetupPortal(&wifiCfg, &netCfg);
    return;
  }

  mqtt.begin(
      creds,
      &runtimeCfg,
      &relay,
      &limits,
      netCfg.mqttHost,
      netCfg.mqttPort);
  Serial.println("[boot] pronto — aguardando comandos MQTT");
}

void loop() {
  esp_task_wdt_reset();

  while (Serial.available()) {
    handleSerialLine(Serial.readStringUntil('\n'));
  }

  if (WiFi.status() != WL_CONNECTED) {
    wifiReady = connectWifi(wifiCfg);
    if (wifiReady) {
      syncTime();
    } else {
      delay(2000);
      return;
    }
  }

  if (!creds.valid) {
    delay(2000);
    return;
  }

  mqtt.loop();
}

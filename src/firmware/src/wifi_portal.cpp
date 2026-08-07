#include "wifi_portal.h"

#include <DNSServer.h>
#include <WebServer.h>
#include <WiFi.h>
#include <esp_task_wdt.h>

#include "domus_config.h"

namespace {
DNSServer dns;
WebServer server(80);

String htmlEscape(const String& in) {
  String out;
  out.reserve(in.length());
  for (size_t i = 0; i < in.length(); ++i) {
    const char c = in[i];
    if (c == '&') {
      out += "&amp;";
    } else if (c == '<') {
      out += "&lt;";
    } else if (c == '>') {
      out += "&gt;";
    } else if (c == '"') {
      out += "&quot;";
    } else {
      out += c;
    }
  }
  return out;
}

String pageForm(const WifiConfig* wifiHint, const NetworkConfig* netHint) {
  const String ssid = wifiHint && wifiHint->valid ? String(wifiHint->ssid) : "";
  const String api = netHint && netHint->valid ? String(netHint->apiBaseUrl) : String("http://192.168.1.10:8080");
  const String mqtt = netHint && netHint->valid ? String(netHint->mqttHost) : String("192.168.1.10");
  const String port =
      netHint && netHint->valid ? String(netHint->mqttPort) : String("1883");

  String html;
  html.reserve(1800);
  html += F("<!DOCTYPE html><html><head><meta charset=utf-8><meta name=viewport "
            "content=\"width=device-width,initial-scale=1\">"
            "<title>Domus Setup</title><style>"
            "body{font-family:system-ui,sans-serif;margin:1.25rem;background:#0f172a;color:#e2e8f0}"
            "h1{font-size:1.35rem;margin:0 0 .25rem}"
            "p{color:#94a3b8;margin:0 0 1rem;font-size:.9rem}"
            "label{display:block;margin:.7rem 0 .25rem;font-size:.85rem;color:#cbd5e1}"
            "input{width:100%;box-sizing:border-box;padding:.65rem;border-radius:.5rem;"
            "border:1px solid #334155;background:#1e293b;color:#f8fafc}"
            "button{margin-top:1.2rem;width:100%;padding:.8rem;border:0;border-radius:.5rem;"
            "background:#38bdf8;color:#0f172a;font-weight:700}"
            "</style></head><body>");
  html += F("<h1>Domus</h1><p>Instala&ccedil;&atilde;o do port&atilde;o &mdash; Wi&#8209;Fi e ativação</p>");
  html += F("<form method=POST action=/save>");
  html += F("<label>Wi&#8209;Fi SSID</label><input name=ssid required value=\"");
  html += htmlEscape(ssid);
  html += F("\"><label>Senha Wi&#8209;Fi</label><input name=password type=password>");
  html += F("<label>URL da API</label><input name=api required value=\"");
  html += htmlEscape(api);
  html += F("\"><label>Host MQTT</label><input name=mqtt required value=\"");
  html += htmlEscape(mqtt);
  html += F("\"><label>Porta MQTT</label><input name=port type=number value=\"");
  html += htmlEscape(port);
  html += F("\"><label>C&oacute;digo de ativa&ccedil;&atilde;o</label>"
            "<input name=code placeholder=\"do app Domus\">");
  html += F("<button type=submit>Salvar e reiniciar</button></form>");
  html += F("<p style=\"margin-top:1.5rem;font-size:.75rem\">Firmware ");
  html += FIRMWARE_VERSION;
  html += F("</p></body></html>");
  return html;
}

void handleRoot(const WifiConfig* wifiHint, const NetworkConfig* netHint) {
  server.send(200, "text/html", pageForm(wifiHint, netHint));
}

void handleSave() {
  WifiConfig wifi{};
  NetworkConfig net{};

  const String ssid = server.arg("ssid");
  const String password = server.arg("password");
  const String api = server.arg("api");
  const String mqtt = server.arg("mqtt");
  const String portStr = server.arg("port");
  const String code = server.arg("code");

  if (ssid.isEmpty() || api.isEmpty() || mqtt.isEmpty()) {
    server.send(400, "text/plain", "SSID, API e MQTT sao obrigatorios");
    return;
  }

  strncpy(wifi.ssid, ssid.c_str(), sizeof(wifi.ssid) - 1);
  strncpy(wifi.password, password.c_str(), sizeof(wifi.password) - 1);
  wifi.valid = true;

  strncpy(net.apiBaseUrl, api.c_str(), sizeof(net.apiBaseUrl) - 1);
  strncpy(net.mqttHost, mqtt.c_str(), sizeof(net.mqttHost) - 1);
  net.mqttPort = static_cast<uint16_t>(portStr.toInt());
  if (net.mqttPort == 0) {
    net.mqttPort = 1883;
  }
  strncpy(net.provisioningCode, code.c_str(), sizeof(net.provisioningCode) - 1);
  net.valid = true;

  saveWifiConfig(wifi);
  saveNetworkConfig(net);

  server.send(
      200,
      "text/html",
      F("<!DOCTYPE html><html><body style=\"font-family:system-ui;background:#0f172a;color:#e2e8f0;"
        "padding:2rem\"><h1>Salvo</h1><p>Reiniciando o Domus...</p></body></html>"));
  delay(500);
  ESP.restart();
}

void handleCaptive() {
  server.sendHeader("Location", String("http://") + WiFi.softAPIP().toString() + "/", true);
  server.send(302, "text/plain", "");
}
}  // namespace

bool setupButtonHeldAtBoot() {
  pinMode(PIN_SETUP_BUTTON, INPUT_PULLUP);
  if (digitalRead(PIN_SETUP_BUTTON) != LOW) {
    return false;
  }
  Serial.println("[setup] botão BOOT pressionado — segure para portal");
  const uint32_t start = millis();
  while (digitalRead(PIN_SETUP_BUTTON) == LOW) {
    if (millis() - start >= SETUP_BUTTON_HOLD_MS) {
      Serial.println("[setup] forçando portal de instalação");
      return true;
    }
    delay(20);
    esp_task_wdt_reset();
  }
  return false;
}

void runSetupPortal(const WifiConfig* wifiHint, const NetworkConfig* netHint) {
  const String apName = String(DOMUS_SETUP_AP_PREFIX) + "-" +
                        String(static_cast<uint16_t>(ESP.getEfuseMac() & 0xFFFF), HEX);

  WiFi.mode(WIFI_AP);
  WiFi.softAP(apName.c_str());
  delay(100);

  const IPAddress ip = WiFi.softAPIP();
  Serial.printf("[setup] SoftAP %s IP %s\n", apName.c_str(), ip.toString().c_str());
  Serial.println("[setup] Conecte o celular e abra http://192.168.4.1");

  dns.start(53, "*", ip);

  server.on("/", HTTP_GET, [wifiHint, netHint]() { handleRoot(wifiHint, netHint); });
  server.on("/save", HTTP_POST, handleSave);
  server.onNotFound(handleCaptive);
  server.begin();

  while (true) {
    dns.processNextRequest();
    server.handleClient();
    delay(2);
    esp_task_wdt_reset();
  }
}

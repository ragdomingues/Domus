#include "domus_mqtt.h"

#include <ArduinoJson.h>
#include <esp_task_wdt.h>
#include <time.h>

#include "domus_config.h"
#include "mqtt_ca_certs.h"
#include "ota_update.h"
#include "uuid_util.h"

DomusMqtt* DomusMqtt::instance_ = nullptr;

namespace {
bool parseIsoUtcToEpoch(const char* iso, time_t& out) {
  if (!iso || strlen(iso) < 19) {
    return false;
  }
  int Y = 0, M = 0, D = 0, h = 0, m = 0, s = 0;
  if (sscanf(iso, "%d-%d-%dT%d:%d:%d", &Y, &M, &D, &h, &m, &s) < 6) {
    return false;
  }
  struct tm t {};
  t.tm_year = Y - 1900;
  t.tm_mon = M - 1;
  t.tm_mday = D;
  t.tm_hour = h;
  t.tm_min = m;
  t.tm_sec = s;
  out = mktime(&t);
  return out > 0;
}

bool isExpired(const char* expiresAtIso) {
  if (!expiresAtIso || !*expiresAtIso) {
    return false;
  }
  time_t expires = 0;
  if (!parseIsoUtcToEpoch(expiresAtIso, expires)) {
    return false;
  }
  const time_t now = time(nullptr);
  if (now < 100000) {
    return false;
  }
  return now > expires;
}
}  // namespace

void DomusMqtt::configureTls() {
#if defined(DOMUS_MQTT_TLS)
#if defined(DOMUS_MQTT_TLS_INSECURE)
  Serial.println("[mqtt] TLS sem validação de CA (INSECURE)");
  net_.setInsecure();
#elif defined(MQTT_CA_CERT)
  Serial.println("[mqtt] TLS com MQTT_CA_CERT (secrets)");
  net_.setCACert(MQTT_CA_CERT);
#else
  Serial.println("[mqtt] TLS com ISRG Root X1");
  net_.setCACert(DOMUS_DEFAULT_MQTT_CA);
#endif
#endif
}

void DomusMqtt::begin(
    const DeviceCredentials& creds,
    RuntimeConfig* cfg,
    RelayDriver* relay,
    LimitSwitch* limits,
    const char* mqttHost,
    uint16_t mqttPort) {
  creds_ = creds;
  cfg_ = cfg;
  relay_ = relay;
  limits_ = limits;
  instance_ = this;
  strncpy(mqttHost_, mqttHost ? mqttHost : "", sizeof(mqttHost_) - 1);
  mqttPort_ = mqttPort == 0 ? 1883 : mqttPort;

  configureTls();

  mqtt_.begin(mqttHost_, mqttPort_, net_);
  mqtt_.setKeepAlive(MQTT_KEEPALIVE_SEC);
  mqtt_.onMessageAdvanced(DomusMqtt::onMessage);

  if (limits_ && limits_->enabled()) {
    const GateState settled = limits_->settledState();
    if (settled == GateState::Open || settled == GateState::Closed) {
      lastState_ = settled;
    }
  }
}

bool DomusMqtt::connected() const {
  return const_cast<MQTTClient&>(mqtt_).connected();
}

void DomusMqtt::loop() {
  if (pendingOtaUrl_[0] != '\0') {
    char url[256];
    strncpy(url, pendingOtaUrl_, sizeof(url) - 1);
    url[sizeof(url) - 1] = '\0';
    pendingOtaUrl_[0] = '\0';
    performOtaUpdate(url);
  }

  if (!mqtt_.connected()) {
    ensureConnected();
  } else {
    mqtt_.loop();
  }

  const uint32_t intervalMs =
      static_cast<uint32_t>((cfg_ ? cfg_->heartbeatIntervalSeconds : DEFAULT_HEARTBEAT_SEC) * 1000UL);
  if (millis() - lastHeartbeat_ >= intervalMs) {
    publishHeartbeat();
  }

  pollLimitSwitches();
}

void DomusMqtt::pollLimitSwitches() {
  if (!limits_ || !limits_->enabled()) {
    return;
  }
  if (millis() - lastLimitPoll_ < 200) {
    return;
  }
  lastLimitPoll_ = millis();

  const GateState settled = limits_->settledState();
  if ((settled == GateState::Open || settled == GateState::Closed) && settled != lastState_) {
    Serial.printf("[limit] mudança → %s\n", gateStateToMqtt(settled));
    publishStatus(settled, nullptr);
  }
}

void DomusMqtt::ensureConnected() {
  if (millis() - lastReconnectAttempt_ < MQTT_RECONNECT_MS) {
    return;
  }
  lastReconnectAttempt_ = millis();

  const String clientId = String("domus-") + creds_.deviceId;
  Serial.printf("[mqtt] connecting as %s...\n", creds_.mqttUsername);

  const bool ok = mqtt_.connect(clientId.c_str(), creds_.mqttUsername, creds_.mqttPassword);
  if (!ok) {
    Serial.println("[mqtt] failed");
    return;
  }

  Serial.println("[mqtt] connected");
  subscribeTopics();
  publishHeartbeat();
  publishStatus(lastState_, nullptr);
}

void DomusMqtt::subscribeTopics() {
  mqtt_.subscribe(creds_.topicCommand, 1);
  mqtt_.subscribe(creds_.topicConfig, 1);
  Serial.printf("[mqtt] sub qos1 %s\n", creds_.topicCommand);
  Serial.printf("[mqtt] sub qos1 %s\n", creds_.topicConfig);
}

void DomusMqtt::onMessage(MQTTClient* /*client*/, char topic[], char bytes[], int length) {
  if (!instance_) {
    return;
  }
  static char buf[1024];
  if (length >= static_cast<int>(sizeof(buf))) {
    length = static_cast<int>(sizeof(buf) - 1);
  }
  memcpy(buf, bytes, static_cast<size_t>(length));
  buf[length] = '\0';
  instance_->handleMessage(topic, buf);
}

void DomusMqtt::handleMessage(const char* topic, const char* payload) {
  if (strcmp(topic, creds_.topicCommand) == 0) {
    handleCommand(payload);
    return;
  }
  if (strcmp(topic, creds_.topicConfig) == 0) {
    handleConfig(payload);
  }
}

GateState DomusMqtt::waitForMotionResult(CommandAction action, const char* commandId) {
  const uint16_t timeoutSec =
      cfg_ && cfg_->commandTimeoutSeconds > 0 ? cfg_->commandTimeoutSeconds : DEFAULT_COMMAND_TIMEOUT_SEC;
  const uint32_t deadline = millis() + static_cast<uint32_t>(timeoutSec) * 1000UL;

  if (!limits_ || !limits_->enabled()) {
    delay(300);
    if (action == CommandAction::Open) {
      return GateState::Open;
    }
    if (action == CommandAction::Close) {
      return GateState::Closed;
    }
    return GateState::Unknown;
  }

  while (millis() < deadline) {
    esp_task_wdt_reset();
    mqtt_.loop();

    const GateState settled = limits_->settledState();
    if (action == CommandAction::Open && settled == GateState::Open) {
      return GateState::Open;
    }
    if (action == CommandAction::Close && settled == GateState::Closed) {
      return GateState::Closed;
    }
    if (action == CommandAction::Stop &&
        (settled == GateState::Open || settled == GateState::Closed)) {
      return settled;
    }
    delay(40);
  }

  Serial.println("[cmd] timeout aguardando fim de curso");
  const GateState settled = limits_->settledState();
  if (settled == GateState::Open || settled == GateState::Closed) {
    return settled;
  }
  (void)commandId;
  return GateState::Unknown;
}

void DomusMqtt::handleCommand(const char* payload) {
  JsonDocument doc;
  if (deserializeJson(doc, payload)) {
    Serial.println("[cmd] JSON inválido");
    return;
  }

  const char* messageId = doc["messageId"] | "";
  if (!*messageId) {
    Serial.println("[cmd] messageId obrigatório — ignorado");
    return;
  }
  if (dedupe_.seen(messageId)) {
    Serial.println("[cmd] duplicado — ignorado");
    return;
  }

  const char* expiresAt = doc["expiresAt"] | "";
  if (isExpired(expiresAt)) {
    Serial.println("[cmd] expirado — ignorado");
    dedupe_.remember(messageId);
    return;
  }

  const char* actionStr = doc["action"] | "";
  const char* commandId = doc["commandId"] | "";
  const CommandAction action = parseAction(actionStr);
  if (action == CommandAction::None) {
    Serial.println("[cmd] action desconhecida");
    return;
  }

  if (!relay_ || !cfg_) {
    return;
  }

  dedupe_.remember(messageId);

  if (!relay_->pulse(action, *cfg_)) {
    Serial.println("[cmd] ação não suportada na config");
    publishStatus(lastState_, commandId);
    return;
  }

  lastState_ = GateState::Moving;
  publishStatus(lastState_, commandId);

  const GateState finalState = waitForMotionResult(action, commandId);
  publishStatus(finalState, commandId);
  Serial.printf("[cmd] %s → %s commandId=%s\n", actionStr, gateStateToMqtt(finalState), commandId);
}

void DomusMqtt::handleConfig(const char* payload) {
  JsonDocument doc;
  if (deserializeJson(doc, payload)) {
    return;
  }

  const char* messageId = doc["messageId"] | "";
  if (!*messageId) {
    Serial.println("[cfg] messageId obrigatório — ignorado");
    return;
  }
  if (dedupe_.seen(messageId)) {
    return;
  }
  dedupe_.remember(messageId);

  if (!cfg_) {
    return;
  }

  if (doc["relayPulseMs"].is<int>()) {
    cfg_->relayPulseMs = static_cast<uint16_t>(doc["relayPulseMs"].as<int>());
  }
  if (doc["heartbeatIntervalSeconds"].is<int>()) {
    cfg_->heartbeatIntervalSeconds = static_cast<uint16_t>(doc["heartbeatIntervalSeconds"].as<int>());
  }
  if (doc["commandTimeoutSeconds"].is<int>()) {
    cfg_->commandTimeoutSeconds = static_cast<uint16_t>(doc["commandTimeoutSeconds"].as<int>());
  }
  if (doc["supportsClose"].is<bool>()) {
    cfg_->supportsClose = doc["supportsClose"].as<bool>();
  }
  if (doc["supportsStop"].is<bool>()) {
    cfg_->supportsStop = doc["supportsStop"].as<bool>();
  }

  saveRuntimeConfig(*cfg_);
  Serial.println("[cfg] runtime atualizado");

  const char* otaUrl = doc["otaUrl"] | "";
  if (*otaUrl) {
    char lastUrl[256]{};
    if (loadLastOtaUrl(lastUrl, sizeof(lastUrl)) && strcmp(lastUrl, otaUrl) == 0) {
      Serial.println("[cfg] OTA ignorado (URL já aplicada)");
      return;
    }
    strncpy(pendingOtaUrl_, otaUrl, sizeof(pendingOtaUrl_) - 1);
    pendingOtaUrl_[sizeof(pendingOtaUrl_) - 1] = '\0';
    Serial.printf("[cfg] OTA agendado: %s\n", pendingOtaUrl_);
  }
}

void DomusMqtt::publishHeartbeat() {
  lastHeartbeat_ = millis();
  if (!mqtt_.connected()) {
    return;
  }

  char messageId[37];
  generateUuidV4(messageId);

  char reportedAt[32];
  const time_t now = time(nullptr);
  strftime(reportedAt, sizeof(reportedAt), "%Y-%m-%dT%H:%M:%SZ", gmtime(&now));

  JsonDocument doc;
  doc["messageId"] = messageId;
  doc["firmwareVersion"] = FIRMWARE_VERSION;
  doc["uptimeSeconds"] = static_cast<uint32_t>(millis() / 1000UL);
  doc["reportedAt"] = reportedAt;

  char body[256];
  serializeJson(doc, body, sizeof(body));
  mqtt_.publish(creds_.topicHeartbeat, body, false, 0);
}

void DomusMqtt::publishStatus(GateState state, const char* commandId) {
  lastState_ = state;
  if (!mqtt_.connected()) {
    return;
  }

  char messageId[37];
  generateUuidV4(messageId);

  char reportedAt[32];
  const time_t now = time(nullptr);
  strftime(reportedAt, sizeof(reportedAt), "%Y-%m-%dT%H:%M:%SZ", gmtime(&now));

  JsonDocument doc;
  doc["messageId"] = messageId;
  doc["state"] = gateStateToMqtt(state);
  if (commandId && *commandId) {
    doc["commandId"] = commandId;
  } else {
    doc["commandId"] = nullptr;
  }
  doc["reportedAt"] = reportedAt;

  char body[320];
  serializeJson(doc, body, sizeof(body));
  mqtt_.publish(creds_.topicStatus, body, true, 1);
}

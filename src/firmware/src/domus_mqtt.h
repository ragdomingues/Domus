#pragma once

#include <MQTT.h>
#include <WiFiClient.h>
#if defined(DOMUS_MQTT_TLS)
#include <WiFiClientSecure.h>
#endif
#include "device_store.h"
#include "gate_types.h"
#include "limit_switch.h"
#include "message_dedupe.h"
#include "relay_driver.h"

class DomusMqtt {
 public:
  void begin(
      const DeviceCredentials& creds,
      RuntimeConfig* cfg,
      RelayDriver* relay,
      LimitSwitch* limits,
      const char* mqttHost,
      uint16_t mqttPort);
  void loop();
  bool connected() const;
  void publishHeartbeat();
  void publishStatus(GateState state, const char* commandId);

 private:
  void ensureConnected();
  void subscribeTopics();
  void configureTls();
  void pollLimitSwitches();
  GateState waitForMotionResult(CommandAction action, const char* commandId);
  static void onMessage(MQTTClient* client, char topic[], char bytes[], int length);
  void handleMessage(const char* topic, const char* payload);
  void handleCommand(const char* payload);
  void handleConfig(const char* payload);

  DeviceCredentials creds_{};
  RuntimeConfig* cfg_ = nullptr;
  RelayDriver* relay_ = nullptr;
  LimitSwitch* limits_ = nullptr;
  MessageDedupe dedupe_{};
  char mqttHost_[64]{};
  uint16_t mqttPort_ = 1883;
  char pendingOtaUrl_[256]{};

#if defined(DOMUS_MQTT_TLS)
  WiFiClientSecure net_;
#else
  WiFiClient net_;
#endif
  MQTTClient mqtt_{1024};

  uint32_t lastReconnectAttempt_ = 0;
  uint32_t lastHeartbeat_ = 0;
  uint32_t lastLimitPoll_ = 0;
  GateState lastState_ = GateState::Unknown;

  static DomusMqtt* instance_;
};

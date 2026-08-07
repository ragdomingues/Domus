#include "relay_driver.h"
#include "domus_config.h"

void RelayDriver::begin() {
  pinMode(PIN_RELAY_OPEN, OUTPUT);
  digitalWrite(PIN_RELAY_OPEN, RELAY_ACTIVE_LEVEL == HIGH ? LOW : HIGH);

#if !DOMUS_SINGLE_RELAY
  pinMode(PIN_RELAY_CLOSE, OUTPUT);
  digitalWrite(PIN_RELAY_CLOSE, RELAY_ACTIVE_LEVEL == HIGH ? LOW : HIGH);
#endif

  pinMode(PIN_RELAY_STOP, OUTPUT);
  digitalWrite(PIN_RELAY_STOP, RELAY_ACTIVE_LEVEL == HIGH ? LOW : HIGH);

  pinMode(PIN_STATUS_LED, OUTPUT);
  digitalWrite(PIN_STATUS_LED, LOW);
}

void RelayDriver::pulsePin(uint8_t pin, uint16_t ms) {
  digitalWrite(PIN_STATUS_LED, HIGH);
  digitalWrite(pin, RELAY_ACTIVE_LEVEL);
  delay(ms);
  digitalWrite(pin, RELAY_ACTIVE_LEVEL == HIGH ? LOW : HIGH);
  digitalWrite(PIN_STATUS_LED, LOW);
}

bool RelayDriver::pulse(CommandAction action, const RuntimeConfig& cfg) {
  const uint16_t ms = cfg.relayPulseMs == 0 ? DEFAULT_RELAY_PULSE_MS : cfg.relayPulseMs;

  switch (action) {
    case CommandAction::Open:
      pulsePin(PIN_RELAY_OPEN, ms);
      return true;
    case CommandAction::Close:
      if (!cfg.supportsClose) {
        return false;
      }
#if DOMUS_SINGLE_RELAY
      // Central Rossi típica: mesmo contato de start/toggle
      pulsePin(PIN_RELAY_OPEN, ms);
#else
      pulsePin(PIN_RELAY_CLOSE, ms);
#endif
      return true;
    case CommandAction::Stop:
      if (!cfg.supportsStop) {
        return false;
      }
      pulsePin(PIN_RELAY_STOP, ms);
      return true;
    default:
      return false;
  }
}

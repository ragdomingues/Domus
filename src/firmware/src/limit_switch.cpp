#include "limit_switch.h"
#include "domus_config.h"

void LimitSwitch::begin() {
#if DOMUS_LIMIT_SWITCHES
  pinMode(PIN_LIMIT_OPEN, INPUT_PULLUP);
  pinMode(PIN_LIMIT_CLOSED, INPUT_PULLUP);
  stableOpen_ = readRawOpen();
  stableClosed_ = readRawClosed();
  lastOpen_ = stableOpen_;
  lastClosed_ = stableClosed_;
  lastChangeMs_ = millis();
  Serial.printf(
      "[limit] habilitado open=GPIO%d closed=GPIO%d (active=%s)\n",
      PIN_LIMIT_OPEN,
      PIN_LIMIT_CLOSED,
      LIMIT_ACTIVE_LEVEL == LOW ? "LOW" : "HIGH");
#else
  Serial.println("[limit] desabilitado (estado inferido)");
#endif
}

bool LimitSwitch::enabled() const {
#if DOMUS_LIMIT_SWITCHES
  return true;
#else
  return false;
#endif
}

bool LimitSwitch::readRawOpen() const {
#if DOMUS_LIMIT_SWITCHES
  return digitalRead(PIN_LIMIT_OPEN) == LIMIT_ACTIVE_LEVEL;
#else
  return false;
#endif
}

bool LimitSwitch::readRawClosed() const {
#if DOMUS_LIMIT_SWITCHES
  return digitalRead(PIN_LIMIT_CLOSED) == LIMIT_ACTIVE_LEVEL;
#else
  return false;
#endif
}

GateState LimitSwitch::settledState() {
#if !DOMUS_LIMIT_SWITCHES
  return GateState::Unknown;
#else
  const bool rawOpen = readRawOpen();
  const bool rawClosed = readRawClosed();
  const uint32_t now = millis();

  if (rawOpen != lastOpen_ || rawClosed != lastClosed_) {
    lastOpen_ = rawOpen;
    lastClosed_ = rawClosed;
    lastChangeMs_ = now;
  } else if (now - lastChangeMs_ >= LIMIT_DEBOUNCE_MS) {
    stableOpen_ = rawOpen;
    stableClosed_ = rawClosed;
  }

  if (stableOpen_ && stableClosed_) {
    return GateState::Unknown;
  }
  if (stableOpen_) {
    return GateState::Open;
  }
  if (stableClosed_) {
    return GateState::Closed;
  }
  return GateState::Unknown;
#endif
}

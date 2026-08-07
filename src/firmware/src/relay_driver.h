#pragma once

#include <Arduino.h>
#include "gate_types.h"
#include "device_store.h"

class RelayDriver {
 public:
  void begin();
  /** Pulsa o canal correspondente. Retorna false se ação não suportada. */
  bool pulse(CommandAction action, const RuntimeConfig& cfg);

 private:
  void pulsePin(uint8_t pin, uint16_t ms);
};

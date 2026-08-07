#pragma once

#include "gate_types.h"

class LimitSwitch {
 public:
  void begin();
  bool enabled() const;

  // OPEN / CLOSED se um fim de curso está estável; UNKNOWN se nenhum ou conflito.
  GateState settledState();

 private:
  bool readRawOpen() const;
  bool readRawClosed() const;

  bool lastOpen_ = false;
  bool lastClosed_ = false;
  uint32_t lastChangeMs_ = 0;
  bool stableOpen_ = false;
  bool stableClosed_ = false;
};

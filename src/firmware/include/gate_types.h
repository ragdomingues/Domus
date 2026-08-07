#pragma once

#include <Arduino.h>

enum class GateState : uint8_t {
  Unknown = 0,
  Closed = 1,
  Open = 2,
  Moving = 3
};

enum class CommandAction : uint8_t {
  None = 0,
  Open = 1,
  Close = 2,
  Stop = 3
};

inline const char* gateStateToMqtt(GateState state) {
  switch (state) {
    case GateState::Open:
      return "OPEN";
    case GateState::Closed:
      return "CLOSED";
    case GateState::Moving:
      return "MOVING";
    default:
      return "UNKNOWN";
  }
}

inline CommandAction parseAction(const char* action) {
  if (!action) {
    return CommandAction::None;
  }
  if (strcasecmp(action, "OPEN") == 0) {
    return CommandAction::Open;
  }
  if (strcasecmp(action, "CLOSE") == 0) {
    return CommandAction::Close;
  }
  if (strcasecmp(action, "STOP") == 0) {
    return CommandAction::Stop;
  }
  return CommandAction::None;
}

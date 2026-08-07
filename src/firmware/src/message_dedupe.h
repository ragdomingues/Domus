#pragma once

#include <Arduino.h>
#include "domus_config.h"

class MessageDedupe {
 public:
  bool seen(const char* messageId);
  void remember(const char* messageId);

 private:
  char cache_[MESSAGE_ID_CACHE_SIZE][37]{};
  uint8_t next_ = 0;
};

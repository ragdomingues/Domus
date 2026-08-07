#include "message_dedupe.h"

bool MessageDedupe::seen(const char* messageId) {
  if (!messageId || !*messageId) {
    return true;
  }
  for (uint8_t i = 0; i < MESSAGE_ID_CACHE_SIZE; i++) {
    if (cache_[i][0] != '\0' && strcmp(cache_[i], messageId) == 0) {
      return true;
    }
  }
  return false;
}

void MessageDedupe::remember(const char* messageId) {
  if (!messageId || !*messageId) {
    return;
  }
  strncpy(cache_[next_], messageId, 36);
  cache_[next_][36] = '\0';
  next_ = static_cast<uint8_t>((next_ + 1) % MESSAGE_ID_CACHE_SIZE);
}

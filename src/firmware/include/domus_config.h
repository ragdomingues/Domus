#pragma once

#include <Arduino.h>

#ifndef FIRMWARE_VERSION
#define FIRMWARE_VERSION "1.1.0"
#endif

// Pinos padrão (ajuste no secrets.h se necessário)
#ifndef PIN_RELAY_OPEN
#define PIN_RELAY_OPEN 26
#endif

#ifndef PIN_RELAY_CLOSE
#define PIN_RELAY_CLOSE 27
#endif

#ifndef PIN_RELAY_STOP
#define PIN_RELAY_STOP 25
#endif

#ifndef PIN_STATUS_LED
#define PIN_STATUS_LED 2
#endif

// Fim de curso (contato seco → GND, INPUT_PULLUP). 0 = desligado.
#ifndef DOMUS_LIMIT_SWITCHES
#define DOMUS_LIMIT_SWITCHES 0
#endif

#ifndef PIN_LIMIT_OPEN
#define PIN_LIMIT_OPEN 32
#endif

#ifndef PIN_LIMIT_CLOSED
#define PIN_LIMIT_CLOSED 33
#endif

// Nível quando o fim de curso está acionado (LOW = contato para GND)
#ifndef LIMIT_ACTIVE_LEVEL
#define LIMIT_ACTIVE_LEVEL LOW
#endif

#ifndef LIMIT_DEBOUNCE_MS
#define LIMIT_DEBOUNCE_MS 40
#endif

#ifndef RELAY_ACTIVE_LEVEL
#define RELAY_ACTIVE_LEVEL HIGH
#endif

#ifndef DOMUS_SINGLE_RELAY
// 1 = OPEN e CLOSE usam o mesmo relé (pulso na central Rossi tipo start/toggle)
#define DOMUS_SINGLE_RELAY 1
#endif

#ifndef MQTT_KEEPALIVE_SEC
#define MQTT_KEEPALIVE_SEC 30
#endif

#ifndef MQTT_RECONNECT_MS
#define MQTT_RECONNECT_MS 5000
#endif

#ifndef DEFAULT_RELAY_PULSE_MS
#define DEFAULT_RELAY_PULSE_MS 500
#endif

#ifndef DEFAULT_HEARTBEAT_SEC
#define DEFAULT_HEARTBEAT_SEC 30
#endif

#ifndef DEFAULT_COMMAND_TIMEOUT_SEC
#define DEFAULT_COMMAND_TIMEOUT_SEC 30
#endif

#ifndef MESSAGE_ID_CACHE_SIZE
#define MESSAGE_ID_CACHE_SIZE 16
#endif

#ifndef WIFI_CONNECT_TIMEOUT_MS
#define WIFI_CONNECT_TIMEOUT_MS 30000
#endif

#ifndef WATCHDOG_TIMEOUT_SEC
#define WATCHDOG_TIMEOUT_SEC 30
#endif

// SoftAP de instalação em campo
#ifndef DOMUS_SETUP_AP_PREFIX
#define DOMUS_SETUP_AP_PREFIX "Domus-Setup"
#endif

#ifndef PIN_SETUP_BUTTON
// GPIO0 (BOOT): segurar ~3s no boot força portal
#define PIN_SETUP_BUTTON 0
#endif

#ifndef SETUP_BUTTON_HOLD_MS
#define SETUP_BUTTON_HOLD_MS 3000
#endif

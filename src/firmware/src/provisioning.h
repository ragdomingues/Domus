#pragma once

#include "device_store.h"

/** POST /api/devices/activate — grava credenciais MQTT one-time na NVS. */
bool activateDevice(const char* apiBaseUrl, const char* provisioningCode, DeviceCredentials& out);

#pragma once

#include "device_store.h"

// SoftAP + formulário HTTP para Wi-Fi, API, MQTT e código de ativação.
// Bloqueia até o usuário salvar (reinicia o ESP).
void runSetupPortal(const WifiConfig* wifiHint, const NetworkConfig* netHint);

bool setupButtonHeldAtBoot();

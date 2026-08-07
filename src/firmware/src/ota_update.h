#pragma once

// Baixa e aplica firmware via HTTP(S). Reinicia em sucesso.
// url: http://.../firmware.bin ou https://...
bool performOtaUpdate(const char* url);

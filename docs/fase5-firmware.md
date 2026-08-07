# FASE 5 — Firmware hardening (revisão)

## Status

**Implementado no código** (`src/firmware` v1.1.0) — validar em hardware.

## Entregue

| Área | Implementação |
|------|----------------|
| Fim de curso | `LimitSwitch` GPIO32/33, env `esp32dev_limits` |
| TLS com CA | ISRG Root X1 + `MQTT_CA_CERT`; lab `esp32dev_tls_insecure` |
| Instalação em campo | SoftAP + formulário (Wi-Fi, API, MQTT, código) |
| OTA | `otaUrl` no tópico `config` → `HTTPUpdate` |
| Serial | `erase-creds` / `erase-wifi` / `erase-all` / `setup` |

## Como validar

1. SoftAP: flash sem Wi-Fi → conectar `Domus-Setup-*` → ativar
2. Comando OPEN/CLOSE com e sem fim de curso
3. `esp32dev_tls` contra broker com cert público
4. Publicar config com `otaUrl` apontando para `.bin` hospedado

# Firmware ESP32 — contrato operacional

Ver também [mqtt-contract.md](mqtt-contract.md) e `src/firmware/README.md`.

## Identidade

- `hardwareId` = `esp32-{MAC}` (efuse)
- `firmwareVersion` = `FIRMWARE_VERSION` (ex.: `1.1.0`) no activate e heartbeat

## Relé / Rossi

| Ação MQTT | Relé (default single) |
|-----------|------------------------|
| OPEN | Pulso `PIN_RELAY_OPEN` |
| CLOSE | Mesmo pin se `DOMUS_SINGLE_RELAY=1` |
| STOP | `PIN_RELAY_STOP` se `supportsStop` |

Duração: `relayPulseMs` (default 500), atualizável via tópico `config`.

## Estados publicados

1. **Com fim de curso** (`DOMUS_LIMIT_SWITCHES=1`): após o pulso publica `MOVING` e aguarda até `commandTimeoutSeconds` o sensor correspondente (`OPEN` / `CLOSED`). Mudanças manuais também são publicadas.
2. **Sem sensores:** após comando bem-sucedido publica `MOVING` e em seguida `OPEN` ou `CLOSED` (ou `UNKNOWN` no STOP) — lifecycle Sent→Delivered→Executed na API.

## Instalação em campo

SoftAP `Domus-Setup-XXXX` grava na NVS: Wi-Fi, URL da API, host/porta MQTT e código de ativação. Alternativa: `secrets.h` + flash USB.

## OTA

Campo opcional `otaUrl` no tópico `config` → HTTP(S) Update → reboot.

## TLS MQTT

Env `esp32dev_tls`: valida CA (ISRG Root X1 ou `MQTT_CA_CERT`). Lab: `esp32dev_tls_insecure`.

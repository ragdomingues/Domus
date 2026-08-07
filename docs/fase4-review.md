# FASE 4 — Firmware ESP32 Rossi (revisão)

## Status

**Concluída — aguardando aprovação.**

Escopo: ESP32 + relé → central Rossi, compatível com MQTT Domus v1, provisioning, commands, heartbeat e (via API) SignalR.

## Entregue

Local: `src/firmware` (PlatformIO + Arduino).

| Área | Implementação |
|------|----------------|
| Provisioning | `POST /api/devices/activate` + persistência NVS |
| MQTT auth | username/password do activate |
| Subscribe | `command` + `config` QoS 1 |
| Publish status | QoS 1 retain true + `messageId` + `commandId` |
| Publish heartbeat | QoS 0 retain false |
| Commands | dedupe `messageId`, `expiresAt`, OPEN/CLOSE/STOP |
| Config remota | pulse, heartbeat, supportsClose/Stop |
| Relé Rossi | pulso configurável; single-relay default |
| Watchdog | task WDT no loop |
| NTP | sync UTC para validar expiresAt |

## Fluxo ponta a ponta

```text
App → POST command → API → MQTT command
  → ESP32 pulsa relé → status MOVING/OPEN|CLOSED
  → API DeviceTelemetryService → SignalR → App
```

## Como validar

1. Subir API + EMQX
2. Criar device Gate + emitir provisioning code
3. Configurar `include/secrets.h`
4. `pio run -e esp32dev -t upload`
5. Monitor serial: activate ok + mqtt connected
6. App/API: enviar OPEN → ver status + SignalR

## Limitações conscientes (FASE 4)

Na FASE 4 o estado era inferido, OTA/TLS CA/SoftAP não existiam.

**Atualização:** ver [fase5-firmware.md](fase5-firmware.md) — fim de curso, TLS com CA, SoftAP e OTA em `src/firmware` v1.1.

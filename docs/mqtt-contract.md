# Contrato MQTT Domus

Broker: **EMQX** (Docker). Abstração `IDeviceMessenger` permite migrar para cloud sem alterar casos de uso.

## Tópicos

```
domus/{tenantId}/{deviceId}/command
domus/{tenantId}/{deviceId}/status
domus/{tenantId}/{deviceId}/heartbeat
domus/{tenantId}/{deviceId}/config
```

## QoS e Retain (obrigatório)

| Tópico | Publisher | QoS | Retain | Motivo |
|--------|-----------|-----|--------|--------|
| `command` | API | **1** | **false** | Entrega ao menos uma vez; comando atrasado não deve ficar retido |
| `config` | API | **1** | **true** | Device novo recebe última config ao subscrever |
| `status` | Device | **1** | **true** | Último estado disponível para API/late subscribers |
| `heartbeat` | Device | **0** | **false** | Alta frequência; perda ocasional aceitável |

Regras:

- Receivers **devem** deduplicar por `messageId` (QoS 1 pode reentregar).
- Mensagens com `expiresAt` no passado **devem** ser ignoradas.
- Retain em `command` é **proibido**.

## MessageId (obrigatório)

Todo payload JSON **deve** incluir `messageId` (UUID v4).

- Gerado pelo publisher.
- Usado para idempotência/dedupe no consumer.
- Distinto de `commandId` (negócio) e `correlationId` (rastreio ponta a ponta).

## Autenticação (FASE 2.2)

EMQX chama HTTP hooks da API:

- `POST /internal/mqtt/auth` — header `X-Domus-Mqtt-Hook`
- `POST /internal/mqtt/acl` — idem

### Device

- Username = `mqttUsername` gerado no activate
- Password = secret one-time do activate (hash SHA-256 no banco)
- Lifecycle deve ser `Active`

### ACL device

| Ação | Tópicos permitidos |
|------|--------------------|
| subscribe | `.../command`, `.../config` (próprio tenant+device) |
| publish | `.../status`, `.../heartbeat` (próprio tenant+device) |

### Conta API (`domus_api`)

| Ação | Tópicos |
|------|---------|
| publish | `.../command`, `.../config` |
| subscribe | `domus/+/+/status`, `domus/+/+/heartbeat` |

## Payloads

### Command (API → Device)

```json
{
  "messageId": "uuid",
  "commandId": "uuid",
  "correlationId": "uuid",
  "action": "OPEN | CLOSE | STOP",
  "issuedAt": "2026-08-06T21:00:00Z",
  "expiresAt": "2026-08-06T21:00:30Z"
}
```

QoS 1, retain false.

### Status (Device → API)

```json
{
  "messageId": "uuid",
  "state": "OPEN | CLOSED | MOVING | UNKNOWN",
  "commandId": "uuid | null",
  "reportedAt": "2026-08-06T21:00:05Z"
}
```

QoS 1, retain true.

### Heartbeat

```json
{
  "messageId": "uuid",
  "firmwareVersion": "1.0.0",
  "uptimeSeconds": 3600,
  "reportedAt": "2026-08-06T21:00:00Z"
}
```

QoS 0, retain false.

### Config (API → Device)

```json
{
  "messageId": "uuid",
  "relayPulseMs": 500,
  "heartbeatIntervalSeconds": 30,
  "commandTimeoutSeconds": 30,
  "supportsClose": true,
  "supportsStop": false,
  "otaUrl": "http://host/path/firmware.bin"
}
```

QoS 1, retain true.

`otaUrl` é opcional: se presente, o ESP32 agenda HTTP(S) OTA e reinicia após sucesso. A API publica `otaUrl` com **retain false** (a config operacional continua retained sem esse campo) para não repetir OTA no reboot.

## Validação Docker (checklist)

Script: [`scripts/validate-mqtt.ps1`](../scripts/validate-mqtt.ps1)

1. `docker compose up -d --build`
2. `/health` (Postgres) e `/health/ready` (Postgres+EMQX)
3. Register/login → create device → issue provisioning → activate
4. Conectar MQTT com credenciais do activate
5. Publicar heartbeat/status → verificar persistência na API (`GET /api/devices/{id}`)
6. Confirmar ACL: publish em tópico de outro device é negado

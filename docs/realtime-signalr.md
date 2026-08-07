# Tempo real — SignalR (contrato v1)

**Schema version:** `1` (`RealtimeContract.SchemaVersion`)  
Todo payload inclui `schemaVersion`. Clientes **devem** ignorar eventos com versão desconhecida ou tratar só campos conhecidos.

## Fluxo

```text
ESP32 → MQTT → subscriber API
            → DeviceTelemetryService (persistência)
            → IDeviceRealtimeNotifier
            → DevicesHub → cliente autorizado (grupos)
```

Presença offline do device: `DevicePresenceWorker` → `DeviceOffline` + `DeviceStatusChanged`.

## Hub

| Item | Valor |
|------|--------|
| Path | `/hubs/devices` |
| Auth | JWT (query `?access_token=` na negociação WebSocket) |
| Auto group | `user:{userId}` no connect |
| Join | `JoinResidence(residenceId)`, `JoinTenant(tenantId)` — exigem membership |
| Leave | `LeaveResidence(residenceId)` |
| Broadcast | grupos `residence:{id}` e `tenant:{id}` do device |

Enums nos payloads são **strings** (`"Online"`, `"Open"`, `"Sent"`, …) — mesmo nome do enum C#.

## Eventos versionados (v1)

### `DeviceStatusChanged`

```json
{
  "schemaVersion": 1,
  "deviceId": "uuid",
  "connectionStatus": "Unknown | Offline | Online",
  "gateState": "Unknown | Closed | Open | Moving | null",
  "reportedAt": "2026-08-06T21:00:00Z"
}
```

### `GateStateChanged`

```json
{
  "schemaVersion": 1,
  "deviceId": "uuid",
  "gateState": "Unknown | Closed | Open | Moving",
  "reportedAt": "2026-08-06T21:00:00Z"
}
```

### `CommandUpdated`

```json
{
  "schemaVersion": 1,
  "commandId": "uuid",
  "deviceId": "uuid",
  "status": "Pending | Sent | Delivered | Executed | Failed | Expired",
  "action": "Open | Close | Stop",
  "failureReason": "string | null"
}
```

### `DeviceOffline`

```json
{
  "schemaVersion": 1,
  "deviceId": "uuid",
  "lastSeenAt": "2026-08-06T21:00:00Z | null"
}
```

## Evolução do contrato

- Breaking change → incrementar `schemaVersion` e documentar aqui.
- Campos novos opcionais podem entrar na mesma versão (forward-compatible).
- Tipos canônicos: `Domus.Application.Realtime.*Payload`.

## Reconnect JWT

Ver [mobile-client.md](mobile-client.md) — access ~15 min; hub deve reconectar com token novo após refresh.

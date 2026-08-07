# FASE 2.4 — SignalR realtime (revisão)

## Status

**Concluída — aguardando aprovação.**

Inclui melhorias pré-2.4 (conflito de comandos, `CommandSource`, validação de Gate).

## 1. Melhorias pré-2.4

| Item | Detalhe |
|------|---------|
| Conflito | OPEN/CLOSE em voo bloqueiam OPEN/CLOSE; STOP permitido durante OPEN/CLOSE |
| `CommandSource` | MobileApp, WebAdmin, Automation, System, API (+ migration) |
| Gate | OPEN se já Open → `gate_already_open`; CLOSE se Closed → `gate_already_closed` |

## 2. SignalR

Pipeline: MQTT subscriber → `DeviceTelemetryService` → persistência → `IDeviceRealtimeNotifier` (`SignalRDeviceRealtimeNotifier`) → grupos autorizados.

| Evento | Origem |
|--------|--------|
| `DeviceStatusChanged` | heartbeat/status (transição online) + presence offline |
| `GateStateChanged` | status MQTT com mudança de estado |
| `CommandUpdated` | create/publish/retry/expire + status com commandId |
| `DeviceOffline` | `DevicePresenceService` / worker |

Hub: `/hubs/devices` (JWT + join residence/tenant).

## 3. Testes

- `CommandConflictRulesTests`, novos casos em `CommandServiceTests`
- `DeviceRealtimeNotifierTests` (telemetry events + presence offline)

## 4. Critério de aceite

- [x] Conflito / Source / Gate pré-validações
- [x] Push SignalR após telemetria/comandos/offline
- [x] Cliente só recebe após join autorizado
- [ ] Validação E2E com app real (fora — RN)

## Fora de escopo

React Native, firmware ESP32, OTA, Push notifications.

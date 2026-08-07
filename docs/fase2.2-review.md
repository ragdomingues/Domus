# FASE 2.2 — Integração EMQX MQTT (revisão)

## Status

**Concluída — aguardando aprovação para FASE 2.3 (Commands API).**

Inclui também os ajustes pré-2.2 aprovados (HardwareId unique, lifecycle, rate limit activate).

## 1. Arquivos alterados

### Ajustes pré-2.2
- `DeviceLifecycleStatus` + `ConnectionStatus` (Unknown/Offline/Online)
- Unique filtrado `HardwareId IS NOT NULL`
- `ActivateAbuseGuard` (IP + code hash + HardwareId)
- Migration `DeviceLifecycleAndHardwareUnique`

### MQTT
- `MqttTopics`, `MqttAuthService`, `DeviceTelemetryService`
- `MqttConnectionService` (publish + subscribe status/heartbeat)
- `MqttDeviceMessenger` (`IDeviceMessenger` real)
- `MqttHookController` (`/internal/mqtt/auth`, `/internal/mqtt/acl`)
- `docker/emqx/emqx.conf` — HTTP auth/ACL → API
- `docker-compose.yml` — ordem api→emqx; env MQTT

## 2. Testes criados

| Teste | Cobertura |
|-------|-----------|
| `DeviceLifecycleTests` | Created→Provisioning→Active→Deleted vs ConnectionStatus |
| `MqttAuthServiceTests` | Device só nos próprios tópicos; cross-tenant negado; service account; suspended |
| `ActivateAbuseGuardTests` | Bloqueio por falhas repetidas de código |

Suite: `dotnet test` (Domain + Application).

## 3. Riscos

| Risco | Nota |
|-------|------|
| EMQX conf HOCON + env custom | Corrigido: template + entrypoint substitui `__DOMUS_MQTT_HOOK_SECRET__` (HOCON não expandia `${DOMUS_MQTT_HOOK_SECRET}`) |
| Dependência api healthy → emqx | `/health` = só Postgres; MQTT reconecta quando EMQX sobe |
| TLS MQTT | Listener TLS preparado; `UseTls=false` em dev |
| Commands API ainda não publica | `IDeviceMessenger` pronto; FASE 2.3 chama PublishCommand |

## 4. Critério de aceite — FASE 2.2

- [x] Device auth via HTTP hook (credenciais do activate)
- [x] ACL: device só publish status/heartbeat e subscribe command/config no próprio tenant/device
- [x] API service account publish command/config + subscribe wildcards
- [x] `IDeviceMessenger` real (MQTTnet)
- [x] Subscriber persiste heartbeat/status (+ Gate state)
- [x] Ajustes HardwareId unique + lifecycle + abuse guard
- [ ] Validação end-to-end com broker real (requer Docker no host)

## Próximo

**FASE 2.3 — Commands API** (idempotency, lifecycle, retry/timeout).

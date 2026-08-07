# FASE 2.3 — Commands API (revisão)

## Status

**Concluída — aguardando aprovação para FASE 2.4 (SignalR push).**

Validações pré-2.3:

- Contrato MQTT documentado (`messageId`, QoS, retain) em [mqtt-contract.md](mqtt-contract.md)
- Validação Docker E2E: **bloqueada neste ambiente** (CLI Docker ausente) — ver [docker-validation.md](docker-validation.md); rodar `.\scripts\validate-mqtt.ps1` no host

## 1. Entregue

| Item | Implementação |
|------|----------------|
| Criação de comando | `POST /api/devices/{deviceId}/commands` |
| Autorização | `IAccessControlService` (tenant/residence) |
| Idempotency key | Índice único `(DeviceId, IdempotencyKey)`; retorno do comando existente |
| Lifecycle | Pending → Sent → Delivered → Executed; Failed; Expired |
| Retry controlado | Até 3 tentativas de publish; `NextRetryAt` + worker |
| Timeout | `ExpiresAt` (body ou `DeviceConfiguration.CommandTimeoutSeconds`) |
| correlationId | UUID por comando; publicado no payload MQTT |
| Persistência | Entidade `Command` + `DeviceEvent` |
| Auditoria | `SecurityAuditAction.CommandCreated` / `CommandFailed` |
| Publicação | `IDeviceMessenger.PublishCommandAsync` (QoS 1, retain false, `messageId`) |

Worker: `CommandProcessingWorker` (retry due + expire timed-out).

Status MQTT com `commandId` avança Sent → Delivered → Executed via `DeviceTelemetryService`.

## 2. Arquivos principais

- `CommandService`, `CommandsController`, `CommandProcessingWorker`
- `Command` (retry/expire/fail)
- `MqttDeviceMessenger` (messageId + correlationId)
- Docs: `mqtt-contract.md`, `docker-validation.md`, script `validate-mqtt.ps1`

## 3. Testes

| Teste | Cobertura |
|-------|-----------|
| `CommandLifecycleTests` | Pending→Executed; expire; retry max; fail send; correlationId |
| `CommandIdempotencyTests` | Lookup por device+key |
| `CommandServiceTests` | Publish/Sent; idempotency; inactive; retry→Failed; timeout→Expired; isolamento |

Suite: `dotnet test` (Domain + Application).

## 4. Critério de aceite — FASE 2.3

- [x] Criar comando autorizado
- [x] Idempotency key
- [x] Lifecycle completo (domínio + status MQTT → Executed)
- [x] Retry controlado + timeout/expire
- [x] correlationId + persistência + auditoria
- [x] Publicação via `IDeviceMessenger`
- [ ] Docker E2E com broker real (host do usuário)

## Fora de escopo

React Native, firmware ESP32, OTA, SignalR push (FASE 2.4).

## Próximo

**FASE 2.4 — SignalR** — concluída; ver [fase2.4-review.md](fase2.4-review.md).

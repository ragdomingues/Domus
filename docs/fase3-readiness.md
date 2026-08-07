# Revisão de prontidão — pré-FASE 3

## Status

**Aprovada** — implementação FASE 3 em [fase3-review.md](fase3-review.md) / `src/mobile`.

Backend IoT principal (FASES 1–2.4) permanece estável.

## Checklist validado

| Item | Status | Evidência |
|------|--------|-----------|
| Contratos SignalR documentados | OK | [realtime-signalr.md](realtime-signalr.md) |
| Payloads versionados (`schemaVersion: 1`) | OK | `RealtimeEventPayloads` + notifier |
| Estratégia reconnect JWT | OK | [mobile-client.md](mobile-client.md) |
| Cenários offline mobile | OK | [mobile-client.md](mobile-client.md) |
| Swagger/API contracts | OK | Bearer no Swagger; [api-contracts.md](api-contracts.md) |

## Ajustes feitos nesta revisão

1. Envelope SignalR v1 com `schemaVersion` em todos os eventos.
2. Docs: realtime (v1), mobile (JWT reconnect + offline), api-contracts.
3. Swagger: OpenAPI v1 + security Bearer JWT.
4. `AccessTokenExpiresAt` alinhado a `Jwt:AccessTokenMinutes` (sem hardcode 15).

## Gaps não bloqueantes (início FASE 3)

- `GET /api/devices/{id}/events` — histórico rico além de commands.
- Endpoints de membership (list/invite/revoke) — tela Usuários.
- ADR 004 atualizado para 4 eventos.

## Escopo FASE 3 (após aprovação)

Android prioritário: login seguro, dashboard residência, status realtime, abrir/fechar, histórico, usuários/permissões conforme API.

**Fora inicialmente:** iOS polish, Push, OTA, Dashboard Web.

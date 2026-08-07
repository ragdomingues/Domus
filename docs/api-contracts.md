# Contratos API REST — Domus v1

Base: HTTPS. Auth: `Authorization: Bearer {accessToken}` (exceto rotas marcadas).  
Erro padrão: `{ "error": "string", "code": "string" }`.  
Swagger UI (Development): `/swagger` com esquema Bearer.

Hub realtime: [realtime-signalr.md](realtime-signalr.md). Cliente: [mobile-client.md](mobile-client.md).

## Auth (`/api/auth`) — anônimas + rate limit

| Método | Rota | Body | Sucesso |
|--------|------|------|---------|
| POST | `/register` | email, password, name, tenantName, residenceName?, timezone? | `AuthTokensResponse` |
| POST | `/login` | email, password, deviceInfo? | `AuthTokensResponse` |
| POST | `/refresh` | refreshToken | `AuthTokensResponse` |
| POST | `/logout` | refreshToken | 204 |
| POST | `/forgot-password` | email | `{ message, resetToken?, expiresAt? }` |
| GET | `/me` | JWT | perfil (`userId`, `email`, `name`, `tenantId?`, `residenceId?`) |
| PATCH | `/me` | JWT | `{ name }` → perfil |
| POST | `/change-password` | JWT | `{ currentPassword, newPassword }` → 204 |
| POST | `/reset-password` | token, newPassword | 204 |

`forgot-password` sempre responde sucesso genérico (anti-enumeração). Em Development (`Auth:ExposeResetToken=true`) o `resetToken` vem no JSON para o app; em produção o token segue por e-mail SMTP (mesmo padrão TopInvest: `SMTP_HOST`, `SMTP_PORT`, `SMTP_SECURE`, `SMTP_USER`, `SMTP_PASS`, `MAIL_FROM`, `APP_PUBLIC_URL`). Sem SMTP configurado, o token é apenas logado.  
`reset-password` códigos: `invalid_reset_token`, `validation_error`. Revoga refresh tokens ativos.

`AuthTokensResponse`: accessToken, refreshToken, accessTokenExpiresAt, refreshTokenExpiresAt, userId, email, name, tenantId, residenceId?

## Residences (`/api`)

| Método | Rota | Papel típico |
|--------|------|----------------|
| POST | `/tenants/{tenantId}/residences` | Owner/Admin tenant |
| GET | `/tenants/{tenantId}/residences` | Membro tenant |
| GET | `/residences/{id}` | Membro residência |
| PUT | `/residences/{id}` | Administrator residência |
| DELETE | `/residences/{id}` | Administrator (soft delete) |

Timezone IANA obrigatório na criação (`America/Sao_Paulo`).

## Members (`/api`) — membership da residência

| Método | Rota | Papel | Notas |
|--------|------|-------|-------|
| GET | `/residences/{id}/members` | Membro ativo | Lista memberships + dados do usuário |
| POST | `/residences/{id}/members` | Administrator | Convite: email, name?, role, validUntilDays? |
| PATCH | `/residences/{id}/members/{membershipId}` | Administrator | Altera role (e validade de Visitor) |
| DELETE | `/residences/{id}/members/{membershipId}` | Administrator | Revoga membership (204) |

`InviteMemberResponse`: membershipId, userId, email, name, role, createdNewUser, temporaryPassword?  
Se o e-mail ainda não existir, cria usuário com senha temporária **retornada uma vez**.  
Códigos: `forbidden`, `already_member`, `not_found`, `invalid_operation`.

## Devices (`/api`)

| Método | Rota | Notas |
|--------|------|--------|
| POST | `/residences/{id}/devices` | Admin; Gate cria config + Gate |
| GET | `/residences/{id}/devices` | Lista |
| GET | `/devices/{id}` | Inclui connectionStatus, gateState, configuration |
| PUT | `/devices/{id}` | Nome |
| PUT | `/devices/{id}/configuration` | pulse, heartbeat, timeout, supportsClose/Stop |
| DELETE | `/devices/{id}` | Soft delete |

`DeviceResponse`: lifecycleStatus, connectionStatus, isProvisioned, lastSeenAt, gateState?, configuration?

## Provisioning

| Método | Rota | Auth |
|--------|------|------|
| POST | `/devices/{id}/provisioning` | JWT admin |
| GET | `/provisioning/{id}` | JWT |
| POST | `/devices/activate` | **Anônimo** (código + hardwareId) |

Activate devolve MQTT credentials **uma vez**.

## Commands (`/api`)

| Método | Rota | Body / notas |
|--------|------|----------------|
| POST | `/devices/{id}/commands` | action, idempotencyKey?, timeoutSeconds?, source? (default MobileApp) |
| GET | `/devices/{id}/commands?take=` | Histórico de comandos (inclui `userName` quando houver) |
| GET | `/devices/{id}/events?take=` | Eventos do device (`DeviceEvent`) |
| GET | `/commands/{id}` | Detalhe |

`CommandResponse`: status, source, correlationId, attemptCount, expiresAt, sent/delivered/executed, failureReason, …

Códigos relevantes: `device_not_active`, `action_not_supported`, `command_conflict`, `gate_already_open`, `gate_already_closed`, `access_denied`.

## Health

| Rota | Significado |
|------|-------------|
| GET `/health` | Postgres |
| GET `/health/ready` | Postgres + EMQX |

## Gaps conhecidos para FASE 3 (UI)

Documentados para implementação no início da FASE 3 (não bloqueiam login/dashboard/comandos básicos):

| Capacidade app | API atual | Gap |
|----------------|-----------|-----|
| Histórico rico (DeviceEvent) | `GET .../commands` + `GET .../events` | — |
| Usuários/permissões UI | `GET/POST/PATCH/DELETE .../members` | — |
| DevicePermission fino | Entidade existe | Sem endpoints públicos |

MVP FASE 3: register/login, members, list devices, SignalR, POST/GET commands.

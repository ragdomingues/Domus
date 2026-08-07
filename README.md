# Domus

Plataforma de automação residencial **multi-tenant**. FASE 1 entrega a fundação: Clean Architecture (.NET 8), PostgreSQL, EMQX, autenticação JWT/Argon2id, modelo IoT (Device/Gate/Provisioning/Commands) e SignalR skeleton.

## Arquitetura (resumo)

```text
Mobile/Web → HTTPS+JWT / SignalR → ASP.NET Core API → PostgreSQL
                                          ↓
                                       EMQX (MQTT TLS)
                                          ↓
                                        ESP32 → Relé → Central Rossi
```

Documentação:

- [docs/architecture.md](docs/architecture.md)
- [docs/security.md](docs/security.md)
- [docs/mqtt-contract.md](docs/mqtt-contract.md)
- [docs/provisioning.md](docs/provisioning.md)
- [docs/realtime-signalr.md](docs/realtime-signalr.md)
- [docs/adr/](docs/adr/)

## Estrutura

```text
Domus/
├── docs/
├── docker/                 # Compose: postgres, emqx, api
├── scripts/                # init-dev.ps1, migrate.ps1
├── src/
│   ├── backend/            # Clean Architecture
│   │   ├── Domus.Api
│   │   ├── Domus.Application
│   │   ├── Domus.Domain
│   │   ├── Domus.Infrastructure
│   │   └── tests/
│   ├── mobile/             # placeholder FASE 3
│   └── firmware/           # placeholder FASE 4
└── .env.example
```

## Pré-requisitos

- .NET 8 SDK
- Docker + Docker Compose (para stack completa)
- (Opcional) `dotnet-ef` para migrations locais

## Executar localmente (Docker)

```powershell
copy .env.example .env
.\scripts\init-dev.ps1
```

Ou:

```powershell
cd docker
docker compose --env-file ..\.env.example up -d --build
```

Endpoints:

| Recurso | URL |
|---------|-----|
| Health | http://127.0.0.1:8080/health |
| Swagger | http://127.0.0.1:8080/swagger |
| Auth | http://127.0.0.1:8080/api/auth/* |
| SignalR | ws://127.0.0.1:8080/hubs/devices |

> No Windows, `http://localhost:8080` pode ficar carregando (IPv6/`::1`). Prefira **`127.0.0.1`**.

`/health` valida **PostgreSQL** e **EMQX**.

### API sem Docker (dev)

1. Suba PostgreSQL e EMQX (Compose só com `postgres` e `emqx`, ou locais).
2. Ajuste `src/backend/Domus.Api/appsettings.Development.json` se necessário.
3. Rode:

```powershell
cd src\backend
dotnet run --project Domus.Api
```

Migrations são aplicadas automaticamente no startup da API.

## Autenticação (FASE 1)

| Método | Rota | Descrição |
|--------|------|-----------|
| POST | `/api/auth/register` | Cria User + Tenant + TenantMembership(Owner) + Residence + ResidenceMembership(Administrator) |
| POST | `/api/auth/login` | Access + Refresh |
| POST | `/api/auth/refresh` | Rotação de refresh (reuse revoga família) |
| POST | `/api/auth/logout` | Revoga família do refresh |

Exemplo register:

```json
{
  "email": "rafael@example.com",
  "password": "SenhaForte1!",
  "name": "Rafael",
  "tenantName": "Rafael",
  "residenceName": "Casa Principal",
  "timezone": "America/Sao_Paulo"
}
```

## Testes

```powershell
cd src\backend
dotnet test Domus.slnx
```

Cobertura FASE 1:

- Membership / visitor expirado
- Command lifecycle (Pending→Executed, Expired, retry)
- DeviceProvisioning
- Residence timezone IANA
- Auth register/login/refresh/logout + refresh reuse

## Segurança (base)

- Argon2id
- JWT curto + refresh com rotação/revogação
- Rate limit em login/register
- Soft delete em User/Tenant/Residence/Device
- `DeviceEvent` (funcional) ≠ `SecurityAuditLog` (segurança)
- Device sem credencial MQTT até provisioning (modelo pronto; activate na FASE 2)

## Fora da FASE 1

- App React Native, firmware ESP32, MQTT real de produção, OTA, Push, Dashboard Web

## Revisão pré-FASE 2

Ver [docs/fase1-review.md](docs/fase1-review.md) — fundação **aprovada** para FASE 2 após hardening (access control, índices, rate limit, testes de isolamento).

## FASE 2.1 — Device Management + Provisioning

**Concluída.** Ver [docs/fase2.1-review.md](docs/fase2.1-review.md).

Fluxo: criar device → emitir provisioning code → `POST /api/devices/activate` → credenciais MQTT (uma vez).

## FASE 2.2 — EMQX MQTT

**Concluída.** Ver [docs/fase2.2-review.md](docs/fase2.2-review.md) e [docs/mqtt-contract.md](docs/mqtt-contract.md).

- Auth/ACL HTTP (`/internal/mqtt/*`)
- `MqttDeviceMessenger` + subscriber status/heartbeat
- Health: `/health` (Postgres), `/health/ready` (Postgres+EMQX)

## FASE 2.3 — Commands API

**Concluída.** Ver [docs/fase2.3-review.md](docs/fase2.3-review.md).

- `POST/GET /api/devices/{id}/commands`, `GET /api/commands/{id}`
- Idempotency, lifecycle, retry/timeout, `correlationId`, auditoria
- Publicação MQTT via `IDeviceMessenger`

Contrato MQTT: [docs/mqtt-contract.md](docs/mqtt-contract.md). Validação Docker: [docs/docker-validation.md](docs/docker-validation.md).

## FASE 2.4 — SignalR realtime

**Concluída.** Ver [docs/fase2.4-review.md](docs/fase2.4-review.md) e [docs/realtime-signalr.md](docs/realtime-signalr.md).

- MQTT → telemetria → persistência → Hub (`DeviceStatusChanged`, `GateStateChanged`, `CommandUpdated`, `DeviceOffline`)
- Pré-2.4: conflito de comandos, `CommandSource`, validação de estado do Gate

## Prontidão FASE 3

Ver [docs/fase3-readiness.md](docs/fase3-readiness.md) — contratos SignalR v1, JWT reconnect, offline mobile, Swagger Bearer, [api-contracts.md](docs/api-contracts.md).

## FASE 3 — App React Native

**Concluída (3.1–3.6).** Ver [docs/fase3-review.md](docs/fase3-review.md) e [src/mobile/README.md](src/mobile/README.md).

```bash
cd src/mobile
cp .env.example .env
npm install
npm run android:dev
```

## FASE 4 — Firmware ESP32 (Rossi)

**Concluída.** Ver [docs/fase4-review.md](docs/fase4-review.md) e [src/firmware/README.md](src/firmware/README.md).

```bash
cd src/firmware
cp include/secrets.h.example include/secrets.h
pio run -e esp32dev -t upload
```

## FASE 5 — Firmware hardening

**No código (v1.1):** fim de curso, TLS com CA, SoftAP de campo, OTA via `otaUrl`. Ver [docs/fase5-firmware.md](docs/fase5-firmware.md).

## Próximos passos

Push ops (lojas), membership/events API, iOS polish — fora do escopo atual.

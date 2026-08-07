# Arquitetura Domus

## Visão

Domus é uma plataforma de automação residencial **multi-tenant**. O primeiro dispositivo concreto é um portão Rossi controlado por ESP32; o domínio nasce genérico (`Device` + tipos) para iluminação, sensores, fechaduras e outros IoT.

## Diagrama lógico

```text
Mobile / Web Admin
        | HTTPS + JWT
        | SignalR (status)
        v
   ASP.NET Core API
        |                \
        v                 v
  PostgreSQL          EMQX (MQTT TLS)
                            |
                            v
                         ESP32
                            |
                          Relé
                            |
                     Central Rossi
```

## Camadas (Clean Architecture)

| Projeto | Responsabilidade |
|---------|------------------|
| `Domus.Domain` | Entidades, enums, invariantes de negócio |
| `Domus.Application` | Casos de uso, DTOs, validadores, interfaces |
| `Domus.Infrastructure` | EF Core, Argon2, JWT, MQTT stub, health checks |
| `Domus.Api` | Controllers, SignalR hubs, middleware, autenticação |

Dependências apontam para dentro: Api → Application/Infrastructure → Domain.

## Multi-tenant e memberships

- `User` **não** possui `TenantId` direto.
- Acesso via `TenantMembership` e `ResidenceMembership`.
- Entidades de negócio (`Residence`, `Device`, …) carregam `TenantId` para isolamento.
- Soft delete em `User`, `Tenant`, `Residence`, `Device` (`DeletedAt`, `DeletedByUserId`).

## Tempo real

ESP32 → MQTT → API → **SignalR** → Mobile. Ver [realtime-signalr.md](realtime-signalr.md).

## Fases

| Fase | Escopo |
|------|--------|
| 1 | Fundação: docs, schema, auth, Docker, health, testes |
| 2 | API devices/commands, MQTT pub/sub, provisioning activate |
| 3 | App React Native |
| 4 (atual) | Firmware ESP32 Rossi |
| 5 | Hardening, CI/CD, deploy |

## Soft delete

Filtros globais EF excluem registros com `DeletedAt != null` nas entidades principais.

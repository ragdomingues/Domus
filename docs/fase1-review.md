# Relatório de revisão técnica — FASE 1 (pré-FASE 2)

**Data:** 2026-08-06  
**Objetivo:** Validar a fundação antes da comunicação real com dispositivos IoT.

---

## Veredito

**Aprovado para iniciar a FASE 2**, após correções aplicadas nesta revisão.

A base está pronta para Device CRUD, Provisioning, EMQX/MQTT e SignalR real, desde que os endpoints IoT usem `IAccessControlService` em todas as operações.

---

## 1. Problemas encontrados

### Segurança

| Severidade | Problema |
|------------|----------|
| High | `DevicesHub.JoinResidence` / `JoinTenant` sem checagem de membership |
| High | Rate limit ausente em `/api/auth/refresh` e `/logout` |
| Medium | Login/refresh ignoravam `ResidenceMembership.IsActiveAt` (visitor expirado) e `Tenant.Status` |
| Medium | Refresh expirado tratado como reuse (revogava família indevidamente) |
| Low | Argon2 `DegreeOfParallelism = 8` (custo alto sob flood) |
| Low | E-mail em plaintext no `SecurityAuditLog` de login fail |

### Banco

| Severidade | Problema |
|------------|----------|
| High | Sem unique em `ProvisioningCodeHash` |
| High | Sem índices `TenantId` / `CreatedAt` em devices, commands, events, provisionings |
| Medium | Unique email/slug impedia reuso após soft delete |
| Medium | `TenantId` denormalizado sem FK em Device/Command/Event/Provisioning |

### Arquitetura

| Severidade | Problema |
|------------|----------|
| Medium (débito) | Application acoplada a EF Core via `IDomusDbContext`/`DbSet` |
| OK | Domain sem deps externas |
| OK | Application não referencia Infrastructure |
| OK | `IDeviceMessenger` + `NullDeviceMessenger` (MQTT desacoplado) |

### Testes

| Severidade | Problema |
|------------|----------|
| Medium | Faltavam testes de IDOR (sem membership / cross-tenant) |
| Medium | Faltava teste de soft-delete filter e idempotency lookup |

---

## 2. Correções realizadas

### Segurança

- Criado `IAccessControlService` / `AccessControlService` (residence, tenant, device + audit `IdorBlocked`).
- `DevicesHub` agora exige membership ativo antes de join em grupos.
- Rate limit `"auth"` aplicado também em refresh e logout.
- Auth usa `IsActiveAt` e tenant `Active`.
- Refresh: expirado → `refresh_expired` (sem matar família); revogado reutilizado → `refresh_reuse` + family kill.
- Argon2 parallelism reduzido para `2`.
- E-mail mascarado em audit de login fail.

### Banco (migration `FoundationHardening`)

- Unique `ProvisioningCodeHash`.
- Índices `TenantId`, `CreatedAt`, compostos `TenantId+CreatedAt`, `UserId` onde faltavam.
- Unique filtrado email/slug: `WHERE "DeletedAt" IS NULL`.
- FK `TenantId` (Restrict) em Device, Command, DeviceEvent, DeviceProvisioning, Notification.

### Testes adicionados

- Sem membership → acesso negado  
- Visitor expirado → acesso negado  
- Cross-tenant device → acesso negado  
- Soft delete filter (User/Device)  
- Idempotency key lookup  
- (já existentes) refresh reuse, provisioning expirado, visitor domain, command lifecycle  

**Resultado:** 21 testes aprovados (`11` Domain + `10` Application).

---

## 3. Checklist de prontidão

| Item | Status |
|------|--------|
| Tenant isolation nas entidades core | OK |
| Acesso sem ResidenceMembership bloqueado (serviço + hub) | OK |
| Refresh rotation + reuse detection | OK |
| Argon2id | OK |
| Secrets fora de logs | OK |
| Soft delete + filtros globais | OK |
| Rate limit em auth sensível | OK |
| Índices TenantId/ResidenceId/DeviceId/UserId/CreatedAt | OK |
| Unique email, slug, provisioning hash, idempotency | OK |
| Domain / Application / IDeviceMessenger | OK |
| MQTT não acoplado a use cases | OK |

---

## 4. Débitos aceitos para FASE 2+

1. Application ainda depende de EF (`DbSet`) — extrair repositórios quando os use cases crescerem.  
2. Race condition em refresh concurrent — considerar lock/transação na FASE 5.  
3. HTTPS redirection / HSTS / ForwardedHeaders — hardening de produção (FASE 5).  
4. JWT signing key default em `appsettings` — obrigar env em produção (FASE 5).  
5. Warnings EF de query filter em navegações filhas de soft-delete — tratar com filtros espelhados ou `IgnoreQueryFilters` conscientes na FASE 2.

---

## 5. Aprovação para FASE 2

**Sim — aprovado para iniciar FASE 2**, com escopo:

- Device CRUD  
- Provisioning real (emit/activate)  
- Integração EMQX (`IDeviceMessenger` real)  
- MQTT publish/subscribe  
- SignalR real (push a partir do subscriber)  
- Commands API (usando `IAccessControlService` + `ICommandIdempotencyService`)

**Ainda não iniciar:** React Native, firmware ESP32, OTA, Push Notification.

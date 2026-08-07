# FASE 2.1 — Device Management + Provisioning (revisão)

## Status

**Concluída — aguardando aprovação para FASE 2.2 (EMQX MQTT).**

## 1. Arquivos alterados / criados

### Application
- `Residences/ResidenceService.cs`, `ResidenceDtos.cs`
- `Devices/DeviceService.cs`, `DeviceDtos.cs`, `ProvisioningService.cs`
- `Security/IAccessControlService.cs` — `EnsureCanManageResidence/Tenant/Device`
- `Abstractions` — `ISecretHasher`, `ISecureTokenGenerator`
- `DependencyInjection.cs`

### Domain
- `Device` — `HardwareId`, `Rename`, `SetHardwareId`, `SetFirmwareVersion`, guard em `ActivateMqttCredentials`
- `Residence` — `Rename`, `SetAddress`
- `SecurityAuditAction` — Residence/Device/ProvisioningFailed

### Infrastructure
- `Security/SecretHasher.cs` — SHA-256 + gerador de tokens
- Migration `AddDeviceHardwareId`
- DI: `ISecretHasher`, `ISecureTokenGenerator`

### API
- `Controllers/ResidencesController.cs`
- `Controllers/DevicesController.cs`
- `Controllers/ProvisioningController.cs`
- `Extensions/HttpContextExtensions.cs`

### Testes
- `DeviceManagementTests.cs`
- `ProvisioningServiceTests.cs`
- `TestFixture.cs`

## 2. Endpoints

| Método | Rota | Auth | Notas |
|--------|------|------|-------|
| POST | `/api/tenants/{tenantId}/residences` | JWT + Tenant Owner/Admin | |
| GET | `/api/tenants/{tenantId}/residences` | JWT + membership | |
| GET/PUT/DELETE | `/api/residences/{id}` | JWT | Manage = Administrator |
| POST/GET | `/api/residences/{id}/devices` | JWT | Create = Administrator |
| GET/PUT/DELETE | `/api/devices/{id}` | JWT | |
| PUT | `/api/devices/{id}/configuration` | JWT manage | |
| POST | `/api/devices/{id}/provisioning` | JWT manage | Retorna code **uma vez** |
| GET | `/api/provisioning/{id}` | JWT manage | Sem secrets |
| POST | `/api/devices/activate` | Anônimo + rate limit | Retorna MQTT password **uma vez** |

## 3. Testes criados (obrigatórios)

| Cenário | Resultado |
|---------|-----------|
| Usuário sem permissão (Member) não cria device | `forbidden` |
| Tenant isolado (cross-tenant GET) | `access_denied` |
| Provisioning expirado | `provisioning_expired` |
| Provisioning reutilizado | `provisioning_reused` |
| Device já ativado (re-issue) | `device_already_activated` |
| Fluxo feliz issue→activate | credenciais geradas; hash ≠ plaintext |

**Suite:** 29 testes aprovados (11 Domain + 18 Application).

## 4. Riscos encontrados

| Risco | Mitigação / débito |
|-------|---------------------|
| Activate anônimo: brute-force de códigos | Rate limit `"auth"`; códigos high-entropy; hash no banco |
| Credencial MQTT em plaintext só no activate | Intencional; GET nunca devolve secret/hash |
| EMQX ainda não consome credenciais | FASE 2.2 (auth/ACL no broker) |
| `DevicePermission` fino ainda não aplicado | Manage = Residence Administrator; Member só lê |
| Soft-delete de residence não cascateia soft-delete de devices | Débito: revogar/soft-delete em lote na FASE 2.x |

## 5. Critério de aceite — FASE 2.1

- [x] CRUD Residences com TenantMembership / ResidenceMembership  
- [x] CRUD Devices + types + DeviceConfiguration + Gate para tipo GATE  
- [x] Provisioning issue → activate → MQTT username/password (hash persistido)  
- [x] SecurityAuditLog em create/update/delete/issue/activate/fail  
- [x] Secrets não retornados em GET  
- [x] Testes obrigatórios passando  
- [x] Sem React Native / firmware / MQTT real / Commands API  

## Próximo passo

Após aprovação desta revisão → **FASE 2.2 — Integração EMQX MQTT**.

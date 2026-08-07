# Segurança Domus

## Autenticação de usuários

- **Access Token JWT** curto (padrão 15 minutos).
- **Refresh Token** com rotação: cada uso emite novo refresh e revoga o anterior.
- Reuso de refresh revogado → revogação da família inteira + `SecurityAuditLog`.
- Senhas com **Argon2id** (nunca plaintext, nunca logadas).
- Rate limiting em `/api/auth/login`, `/register`, `/forgot-password` e `/reset-password`.
- Reset de senha: token one-time hasheado; resposta genérica anti-enumeração; refresh tokens revogados após reset.

## Autorização e isolamento

- Toda operação valida `TenantMembership` e, quando aplicável, `ResidenceMembership` ativo.
- Proteção anti-IDOR: nunca confiar apenas no ID da URL.
- Papéis por residência: `Administrator`, `Member`, `Visitor` (com validade).
- Papéis por tenant: `Owner`, `Admin`, `Member`.

## IoT

- ESP32 **não** aceita comandos HTTP públicos.
- Fluxo: usuário autorizado → API valida → `Command` → MQTT TLS → device.
- Credenciais MQTT **somente após** `DeviceProvisioning` ativado.
- Secrets e códigos de provisioning armazenados como hash.

## Auditoria

| Tipo | Entidade | Exemplos |
|------|----------|----------|
| Funcional | `DeviceEvent` | OPEN/CLOSE, resultado, origem |
| Segurança | `SecurityAuditLog` | login fail, refresh reuse, revogação, IDOR |

## Transporte

- HTTPS obrigatório em produção.
- MQTT com TLS.
- SignalR autenticado com JWT (`access_token` na query do hub).

Reconnect / refresh no cliente: [mobile-client.md](mobile-client.md).

## Secrets

- Variáveis de ambiente / `.env` (nunca commitados).
- Ver `.env.example`.

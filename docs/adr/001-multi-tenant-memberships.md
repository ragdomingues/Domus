# ADR 001 — Multi-tenant via memberships

## Status

Aceito

## Contexto

O produto precisa isolar dados entre clientes e permitir que um usuário acesse várias residências/tenants com papéis diferentes.

## Decisão

- `User` sem `TenantId` direto.
- `TenantMembership` e `ResidenceMembership` definem acesso.
- Entidades de negócio carregam `TenantId` para filtros e isolamento.

## Consequências

Mais joins nas queries; flexibilidade para multi-casa e futuros convites cross-tenant.

# ADR 003 — Argon2id e rotação de refresh

## Status

Aceito

## Contexto

Autenticação precisa resistir a brute force offline e permitir logout/revogação real.

## Decisão

- Hash de senha: Argon2id.
- Access JWT curto + refresh rotativo com família de tokens.
- Reuso de refresh revogado invalida a família e gera `SecurityAuditLog`.

## Consequências

Maior custo de CPU no login (aceitável); sessões controláveis.

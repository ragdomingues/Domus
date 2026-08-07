# ADR 004 — Soft delete e SignalR

## Status

Aceito

## Contexto

Exclusões precisam ser auditáveis; o app precisa de status em tempo real sem polling agressivo.

## Decisão

- Soft delete (`DeletedAt`, `DeletedByUserId`) em User, Tenant, Residence, Device com filtro global EF.
- SignalR hub autenticado; eventos v1 com `schemaVersion`:
  `DeviceStatusChanged`, `GateStateChanged`, `CommandUpdated`, `DeviceOffline`.
- Contrato: [realtime-signalr.md](../realtime-signalr.md).

## Consequências

Queries padrão não veem deletados; restore possível; mobile recebe updates push via hub.
Evolução de payload exige bump de `schemaVersion`.

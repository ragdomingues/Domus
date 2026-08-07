# ADR 002 — EMQX e IDeviceMessenger

## Status

Aceito

## Contexto

Precisamos de MQTT TLS self-hosted agora e migração futura para cloud sem reescrever casos de uso.

## Decisão

- Broker inicial: EMQX no Docker Compose.
- Porta de saída da Application: `IDeviceMessenger`.
- Contrato de tópicos estável (`domus/{tenantId}/{deviceId}/...`).

## Consequências

Troca de broker = nova implementação de Infrastructure; Application permanece estável.

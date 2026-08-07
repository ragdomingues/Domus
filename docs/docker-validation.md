# Validação Docker MQTT — status

## Nota EMQX hook secret

O HOCON do EMQX **não** expande variáveis de ambiente customizadas no arquivo montado.
O Compose usa `emqx.conf.template` + `docker-entrypoint-domus.sh` para substituir
`__DOMUS_MQTT_HOOK_SECRET__` por `DOMUS_MQTT_HOOK_SECRET` antes de subir o broker.

Se a API logar `MQTT NotAuthorized` com header literal `${DOMUS_MQTT_HOOK_SECRET}`,
recree o EMQX:

```powershell
docker compose --env-file ..\.env up -d --force-recreate emqx
```

## Resultado neste ambiente de desenvolvimento

Validação depende do Docker Desktop + WSL2 no host.

## Como validar no seu host

```powershell
copy .env.example .env
.\scripts\validate-mqtt.ps1
```

Checklist manual complementar (após o script):

1. Conectar ao EMQX (`localhost:1883`) com `mqttUsername`/`mqttPassword` do activate.
2. Publicar heartbeat (QoS 0, retain false) com `messageId`.
3. Publicar status (QoS 1, retain true) com `messageId`.
4. `GET /api/devices/{id}` → `connectionStatus=Online`, `firmwareVersion` e `gateState` atualizados.
5. Tentar publish em tópico de outro device → ACL deny.

Ver contrato: [mqtt-contract.md](mqtt-contract.md).

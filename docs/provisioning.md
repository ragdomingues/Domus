# Device Provisioning

Dispositivos **não nascem** com credencial MQTT fixa.

## Entidade

`DeviceProvisioning`: `Id`, `DeviceId`, `TenantId`, `ProvisioningCodeHash`, `Status`, `CreatedAt`, `ExpiresAt`, `ActivatedAt`, `ActivatedFromIp`.

Status: `Pending` | `Activated` | `Expired` | `Revoked`.

## Fluxo

1. Admin cria `Device` (sem `mqttSecret`) e solicita provisioning.
2. API gera código one-time (plaintext só na resposta; hash no banco).
3. ESP32 chama activate com código + hardware id.
4. API valida código, expiração e status; gera `mqttUsername`/`secret`; marca `Activated`.
5. Device conecta ao EMQX com as novas credenciais.

## Firmware (FASE 4)

ESP32 chama `POST /api/devices/activate` no primeiro boot (código em `secrets.h`), persiste MQTT na NVS.  
Detalhes: [firmware-esp32.md](firmware-esp32.md).

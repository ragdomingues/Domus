# Firmware Domus ESP32 — Portão Rossi (v1.1)

Firmware Arduino/PlatformIO para **ESP32** controlando central de portão **Rossi** via relé, alinhado ao contrato MQTT Domus v1.

## Compatibilidade

| Contrato | Comportamento |
|----------|----------------|
| Tópicos `domus/{tenant}/{device}/…` | IDs vindos do activate |
| DeviceProvisioning | `POST /api/devices/activate` → NVS |
| Credenciais MQTT | username/password one-time persistidos |
| Command lifecycle | dedupe `messageId`, ignora `expiresAt`, status com `commandId` |
| Heartbeat | QoS 0, retain false |
| Status | QoS 1, retain true (`OPEN/CLOSED/MOVING/UNKNOWN`) |
| Config | pulse, heartbeat, supportsClose/Stop, **`otaUrl`** |
| Fim de curso | opcional (`DOMUS_LIMIT_SWITCHES`) |
| TLS | CA ISRG Root X1 ou `MQTT_CA_CERT` |
| Instalação em campo | SoftAP `Domus-Setup-XXXX` |

## Hardware

```
ESP32 GPIO26 ──► Relé (NO) ──► contato START/OPEN da central Rossi
     (opcional GPIO27 CLOSE, GPIO25 STOP se DOMUS_SINGLE_RELAY=0)
GPIO32 ◄── fim de curso ABERTO (contato seco → GND)   [opcional]
GPIO33 ◄── fim de curso FECHADO (contato seco → GND)  [opcional]
GND comum com a central (apenas referência do relé seco — não misturar 127/220V no ESP)
```

Padrão `DOMUS_SINGLE_RELAY=1`: OPEN e CLOSE pulsam o mesmo relé (Rossi start/toggle).

**Estado do portão:** com fim de curso (`-e esp32dev_limits` ou `#define DOMUS_LIMIT_SWITCHES 1`) o status reflete os sensores; sem sensores, permanece **inferido** (MOVING → OPEN/CLOSED).

## Build

```bash
cd src/firmware
cp include/secrets.h.example include/secrets.h
# opcional: edite Wi-Fi / API (ou deixe vazio e use SoftAP)

pio run -e esp32dev
pio run -e esp32dev -t upload
pio device monitor
```

| Env | Uso |
|-----|-----|
| `esp32dev` | Dev MQTT plain `:1883` |
| `esp32dev_tls` | MQTT TLS com validação de CA |
| `esp32dev_tls_insecure` | TLS lab (EMQX self-signed) |
| `esp32dev_limits` | Com fim de curso GPIO32/33 |

## Instalação em campo (SoftAP)

1. Flash o firmware (Wi-Fi pode ficar vazio no `secrets.h`)
2. No 1º boot (ou se o Wi-Fi falhar), sobe AP **`Domus-Setup-XXXX`**
3. Celular → conectar → abrir `http://192.168.4.1`
4. Preencher SSID, senha, URL da API, host/porta MQTT e código de ativação
5. Salvar → ESP reinicia, faz activate e conecta ao EMQX

Reabrir portal: segurar **BOOT (GPIO0)** ~3s no boot, ou serial `setup`.

## Provisioning (dev com secrets.h)

1. Admin cria device Gate + `POST .../provisioning` → código
2. Coloque o código em `PROVISIONING_CODE` ou no SoftAP
3. Flash + boot com Wi-Fi
4. Firmware chama activate, grava NVS, conecta EMQX

## Serial

| Comando | Efeito |
|---------|--------|
| `erase-creds` | Apaga credenciais MQTT (re-provisiona) |
| `erase-wifi` | Apaga Wi-Fi da NVS |
| `erase-all` | Apaga rede + MQTT |
| `setup` | Abre SoftAP imediatamente |

## OTA

Publique no tópico `config` (API ou MQTT) um payload com `otaUrl`:

```json
{
  "messageId": "uuid",
  "otaUrl": "http://192.168.1.10:8080/firmware/domus-esp32.bin"
}
```

O device baixa o `.bin` (partição OTA `min_spiffs`) e reinicia. HTTPS usa a mesma CA do TLS MQTT.

## TLS

- Produção: `pio run -e esp32dev_tls` (ISRG Root X1 embutido)
- CA própria: `MQTT_CA_CERT` no `secrets.h`
- Lab docker self-signed: `esp32dev_tls_insecure`

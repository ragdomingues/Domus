# FASE 3 — App React Native (revisão)

## Status

**Concluída (3.1–3.6) — aguardando aprovação.**

Android prioritário. Backend IoT inalterado (sem novos endpoints nesta fase).

## Entregue

### 3.1 Fundação
- Expo + TypeScript em `src/mobile`
- Ambientes `dev` / `homolog` / `production` (`app.config.ts` + scripts npm)
- SecureStore para access/refresh; refresh proativo + interceptor 401
- Logout limpa SecureStore + chama API

### 3.2 Auth
- Login, logout, sessão hidratada na abertura
- Refresh expirado → força login
- Tokens nunca em AsyncStorage

### 3.3 Dashboard
- Lista residências do tenant
- Lista devices (nome, tipo, online/offline, lastSeen)
- SignalR join residence/tenant + patch de estado

### 3.4 Portão
- ABRIR / FECHAR / PARAR (respeita supportsClose/Stop)
- POST command + feedback SignalR / fases UI
- Estados: Aberto, Fechado, Abrindo, Fechando, Parado, Offline

### 3.5 Histórico
- Adapter sobre `GET /devices/{id}/commands`
- Dependência futura `DeviceEvent` documentada

### 3.6 Usuários
- API members (list/invite/update/revoke) + tela com convite e senha temporária one-time
- Criar conta via `POST /api/auth/register` no app

## Segurança / UX
- Stack auth bloqueia rotas privadas
- Banners: Conectando / Offline / Comando enviado / Executando / Concluído / Falhou
- Logs sem tokens

## Como validar
1. API + EMQX no Docker/host
2. `cd src/mobile && npm run android:dev`
3. Login → dashboard → abrir portão → observar realtime/histórico

## Fora de escopo (mantido)
Push, OTA, Web dashboard, automação, iOS polish.

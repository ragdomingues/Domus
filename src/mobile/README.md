# Domus Mobile (FASE 3)

App React Native (Expo) + TypeScript — **Android prioritário**.

## Stack

- React Navigation
- Zustand (sessão + estado de devices/comandos)
- React Query
- Axios (JWT + refresh automático)
- Expo SecureStore (tokens — **nunca** AsyncStorage)
- `@microsoft/signalr` (hub `/hubs/devices`)

## Ambientes

| Script | `EXPO_PUBLIC_APP_ENV` |
|--------|------------------------|
| `npm run start:dev` | dev |
| `npm run start:homolog` | homolog |
| `npm run start:production` | production |

Configure URLs em `.env` (ver `.env.example`) ou `app.config.ts`.

Emulador Android → API no host: `http://10.0.2.2:8080`.

## Rodar

```bash
cd src/mobile
cp .env.example .env
npm install
npm run android:dev
```

Typecheck: `npm run typecheck`

## Estrutura

```
src/
  api/           # HTTP + APIs tipadas
  auth/          # SecureStore + session Zustand
  config/        # env
  features/      # auth, dashboard, gate, history, users
  navigation/
  providers/     # AppProviders (Query/SignalR/session)
  realtime/      # SignalR client
  store/         # devices / command UI
  theme/
  ui/
```

## Pré-requisito Android

O comando `npm run android:dev` precisa do **Android SDK** (`adb`).

1. Instale [Android Studio](https://developer.android.com/studio)
2. No wizard, marque **Android SDK**, **SDK Platform** e **Android Virtual Device**
3. Abra **More Actions → SDK Manager** e confirme Platform-Tools
4. Crie um emulador em **Device Manager** (ex.: Pixel 6, API 34)
5. Defina variáveis de ambiente do usuário:

```text
ANDROID_HOME = %LOCALAPPDATA%\Android\Sdk
Path += %ANDROID_HOME%\platform-tools
Path += %ANDROID_HOME%\emulator
```

6. Feche e reabra o terminal / Cursor
7. Confirme: `adb version`
8. `npm run android:dev`

Sem emulador/SDK, use só o bundler e o app **Expo Go** no celular:

```bash
npm run start:dev
```

(API precisa estar acessível pelo IP da máquina na LAN, não `10.0.2.2`.)

## Fases cobertas

| Fase | Conteúdo |
|------|----------|
| 3.1 | Fundação, envs, tokens seguros, HTTP interceptors |
| 3.2 | Login / logout / sessão persistente |
| 3.3 | Dashboard residências + devices + realtime |
| 3.4 | Controle portão ABRIR/FECHAR/PARAR |
| 3.5 | Histórico via commands (adapter) |
| 3.6 | Tela usuários (dependência API documentada) |

## Contratos

- [api-contracts.md](../../docs/api-contracts.md)
- [realtime-signalr.md](../../docs/realtime-signalr.md)
- [mobile-client.md](../../docs/mobile-client.md)

## Push (lojas / EAS)

Push **não funciona no Expo Go**. Use build EAS (`preview` ou `production`).

### Checklist Android (Play Store)

1. Crie projeto no [Firebase Console](https://console.firebase.google.com/) com package `app.domus.mobile`
2. Baixe `google-services.json` → copie para `src/mobile/google-services.json` (gitignored; há `.example`)
3. Gere **Service Account** FCM V1 (Firebase → Project Settings → Service Accounts)
4. Faça upload no EAS:
   ```bash
   cd src/mobile
   eas credentials
   # Android → production → Google Service Account → FCM V1 → upload
   ```
5. Build e submit:
   ```bash
   eas build --platform android --profile production
   eas submit --platform android --profile production
   ```

### Checklist iOS (App Store)

1. Apple Developer: App ID `app.domus.mobile` com Push Notifications
2. `eas credentials` → iOS → production → configure Push Key (APNs)
3. Build/submit:
   ```bash
   eas build --platform ios --profile production
   eas submit --platform ios --profile production
   ```

### Backend

Com Enhanced Security no Expo, defina `EXPO_ACCESS_TOKEN` no `.env` da API (ver `.env.example` na raiz).

Canal Android: `domus-alerts` (app + backend alinhados).

## Fora do escopo

OTA, Dashboard Web, automação, iOS refinado além do push.

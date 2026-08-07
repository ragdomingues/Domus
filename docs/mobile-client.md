# Cliente mobile — auth, SignalR e offline

Contrato de comportamento esperado do app React Native (FASE 3). Backend já suporta os fluxos descritos.

## Tokens

| Token | TTL padrão | Uso |
|-------|------------|-----|
| Access JWT | 15 min (`Jwt:AccessTokenMinutes`) | `Authorization: Bearer` nas APIs; query `access_token` no hub |
| Refresh | 14 dias | `POST /api/auth/refresh` — **rotação** (refresh antigo invalidado) |

Resposta auth inclui `accessTokenExpiresAt` alinhado à config JWT.

### Armazenamento seguro (Android prioritário)

- Preferir Keystore / EncryptedSharedPreferences (ou lib equivalente no RN).
- Nunca logar tokens.
- Logout → `POST /api/auth/logout` + limpar storage.

## Estratégia de reconnect JWT + SignalR

1. **HTTP 401** → tentar `refresh` uma vez; se ok, repetir request; se falhar → tela de login.
2. **Refresh reuse / família revogada** → logout forçado (segurança).
3. **Proactive refresh:** renovar access ~60–90s antes de `accessTokenExpiresAt`.
4. **SignalR:**
   - Conectar: ` /hubs/devices?access_token={accessJwt}`
   - Após refresh bem-sucedido: **parar hub → reconnect** com o novo JWT (o token da handshake não é renovado automaticamente).
   - Em `onreconnected` / connect: chamar de novo `JoinResidence` (e `JoinTenant` se usado).
   - Em falha de auth no hub: refresh + reconnect; se persistir → login.
5. **Backoff** de reconnect: 1s, 2s, 5s, 10s (cap); reset após connect ok.

```text
API 401 / hub auth fail
  → refresh
  → sucesso: atualizar tokens, reconnect hub, re-join groups
  → falha: clear session → Login
```

## Cenários offline (app)

| Cenário | Comportamento esperado |
|---------|------------------------|
| Sem rede ao abrir app | Mostrar último estado em cache (se houver); banner offline; bloquear comandos novos ou enfileirar com aviso |
| Rede cai durante uso | Banner; hub desconecta; não marcar portão como Offline do device só por queda do app |
| Comando com rede instável | `POST .../commands` pode falhar — retry com mesma `idempotencyKey`; UI mostra status via GET/SignalR quando voltar |
| Access expira offline | Ao voltar online: refresh; se refresh também inválido → login |
| Device ESP32 offline | Eventos `DeviceOffline` / `connectionStatus=Offline`; botões OPEN/CLOSE desabilitados ou com confirmação |
| Conflito / gate redundante | API `command_conflict`, `gate_already_open`, `gate_already_closed` — mensagem clara, sem retry cego |
| App em background | Hub pode dropar; ao foreground: refresh se necessário + reconnect + `GET` device/commands para reconciliar |

### Cache sugerido (FASE 3)

- Última residência selecionada, lista de devices, último `gateState` / `connectionStatus`.
- Histórico recente (commands/events) — stale-while-revalidate ao voltar online.
- **Não** cachear refresh token em plaintext fora do storage seguro.

### Reconciliação

Após reconnect SignalR ou retorno online:

1. `GET /api/devices/{id}` (status atual)
2. `GET /api/devices/{id}/commands?take=20` (comandos em voo)
3. Re-join `JoinResidence`

## Conta e membros (app)

| Fluxo | API |
|-------|-----|
| Criar conta | `POST /api/auth/register` → persiste tokens (mesma sessão do login) |
| Login | `POST /api/auth/login` |
| Esqueci senha | `POST /api/auth/forgot-password` → `POST /api/auth/reset-password` |
| Perfil | `GET/PATCH /api/auth/me`, `POST /api/auth/change-password` |
| Residências CRUD | `POST/PUT/DELETE` residences + formulário no app |
| Devices CRUD / provisioning | `POST/PUT/DELETE` devices + `POST .../provisioning` |
| Histórico | `GET .../commands` + `GET .../events` |

| Listar membros | `GET /api/residences/{id}/members` |
| Convidar | `POST /api/residences/{id}/members` — se `createdNewUser`, mostrar `temporaryPassword` **uma vez** |
| Remover | `DELETE /api/residences/{id}/members/{membershipId}` |

Papéis: `Administrator` \| `Member` \| `Visitor` (Visitor aceita `validUntilDays`).

## Fora do escopo inicial FASE 3

Push Notification, OTA, iOS polish, Dashboard Web.

import type { CommandResponse, DeviceEventResponse } from '../../shared/types/api';
import { devicesApi } from '../../api/devicesApi';
import { parseApiDate } from '../../shared/datetime';

export type HistoryItem = {
  id: string;
  deviceId: string;
  action: string;
  actionLabel: string;
  status: string;
  userId?: string | null;
  userLabel: string;
  createdAt: string;
  resultLabel: string;
  source: string;
  failureReason?: string | null;
  kind: 'command' | 'event';
};

function resultLabel(status: string, failureReason?: string | null): string {
  switch (status) {
    case 'Executed':
    case 'Success':
      return 'Sucesso';
    case 'Failed':
    case 'Failure':
      return failureReason ? `Falhou: ${failureReason}` : 'Falhou';
    case 'Expired':
      return 'Expirado';
    case 'Pending':
    case 'Sent':
    case 'Delivered':
      return 'Em andamento';
    default:
      return status;
  }
}

/** Nomes amigáveis para ações técnicas (OPEN, STATUS_OPEN, …). */
export function actionLabel(action: string, kind: 'command' | 'event'): string {
  const key = action.trim().toUpperCase();
  switch (key) {
    case 'OPEN':
      return kind === 'command' ? 'Abrir portão' : 'Abertura solicitada';
    case 'CLOSE':
      return kind === 'command' ? 'Fechar portão' : 'Fechamento solicitado';
    case 'STOP':
      return 'Parar portão';
    case 'STATUS_OPEN':
    case 'STATUS_OPENED':
      return 'Portão aberto';
    case 'STATUS_CLOSED':
    case 'STATUS_CLOSE':
      return 'Portão fechado';
    case 'STATUS_UNKNOWN':
      return 'Estado desconhecido';
    case 'STATUS_OPENING':
      return 'Portão abrindo';
    case 'STATUS_CLOSING':
      return 'Portão fechando';
    case 'STATUS_STOPPED':
      return 'Portão parado';
    default:
      if (key.startsWith('STATUS_')) {
        return 'Atualização de estado';
      }
      return action;
  }
}

function originLabel(origin?: string | null): string {
  switch ((origin ?? '').toLowerCase()) {
    case 'system':
      return 'Sistema';
    case 'app':
      return 'Aplicativo';
    case 'admin':
      return 'Administração';
    case 'automation':
      return 'Automação';
    default:
      return origin?.trim() || 'Sistema';
  }
}

export function mapCommandToHistory(command: CommandResponse): HistoryItem {
  const action = String(command.action).toUpperCase();
  return {
    id: command.id,
    deviceId: command.deviceId,
    action,
    actionLabel: actionLabel(action, 'command'),
    status: String(command.status),
    userId: command.userId,
    userLabel: command.userName?.trim()
      ? command.userName
      : command.userId
        ? `Usuário ${command.userId.slice(0, 8)}…`
        : 'Sistema',
    createdAt: command.createdAt,
    resultLabel: resultLabel(String(command.status), command.failureReason),
    source: String(command.source),
    failureReason: command.failureReason,
    kind: 'command',
  };
}

export function mapEventToHistory(event: DeviceEventResponse): HistoryItem {
  const action = event.action.toUpperCase();
  return {
    id: event.id,
    deviceId: event.deviceId,
    action,
    actionLabel: actionLabel(action, 'event'),
    status: event.result,
    userId: event.userId,
    userLabel: event.userName?.trim()
      ? event.userName
      : event.userId
        ? `Usuário ${event.userId.slice(0, 8)}…`
        : originLabel(event.origin),
    createdAt: event.createdAt,
    resultLabel: resultLabel(event.result, event.details),
    source: event.origin,
    failureReason: event.details,
    kind: 'event',
  };
}

export async function fetchDeviceHistory(deviceId: string): Promise<HistoryItem[]> {
  const [commands, events] = await Promise.all([
    devicesApi.listCommands(deviceId, 50),
    devicesApi.listEvents(deviceId, 50).catch(() => [] as DeviceEventResponse[]),
  ]);

  const merged = [
    ...commands.map(mapCommandToHistory),
    ...events.map(mapEventToHistory),
  ];

  return merged.sort(
    (a, b) => parseApiDate(b.createdAt).getTime() - parseApiDate(a.createdAt).getTime(),
  );
}

import AsyncStorage from '@react-native-async-storage/async-storage';
import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';
import type { CommandResponse, DeviceResponse, GateState } from '../shared/types/api';

export type CommandUiPhase =
  | 'idle'
  | 'sending'
  | 'sent'
  | 'executing'
  | 'done'
  | 'failed';

type DevicesState = {
  devicesById: Record<string, DeviceResponse>;
  selectedResidenceId: string | null;
  /** userId dono da seleção — evita JoinResidence com residência de outra sessão. */
  selectedResidenceUserId: string | null;
  hubState: 'connecting' | 'connected' | 'reconnecting' | 'disconnected';
  networkOnline: boolean;
  activeCommandId: string | null;
  commandPhase: CommandUiPhase;
  commandMessage: string | null;
  setResidence: (residenceId: string | null, userId?: string | null) => void;
  setDevices: (devices: DeviceResponse[]) => void;
  upsertDevice: (device: DeviceResponse) => void;
  patchDevice: (
    deviceId: string,
    patch: Partial<Pick<DeviceResponse, 'connectionStatus' | 'gateState' | 'lastSeenAt'>>,
  ) => void;
  setHubState: (state: DevicesState['hubState']) => void;
  setNetworkOnline: (online: boolean) => void;
  beginCommand: (command: CommandResponse) => void;
  applyCommandUpdate: (commandId: string, status: string, failureReason?: string | null) => void;
  resetCommandUi: () => void;
  /** Limpa residência/devices ao trocar de usuário (evita JoinResidence em ID stale). */
  resetForSessionChange: () => void;
};

function mapCommandPhase(status: string): CommandUiPhase {
  switch (status) {
    case 'Pending':
    case 'Sent':
      return 'sent';
    case 'Delivered':
      return 'executing';
    case 'Executed':
      return 'done';
    case 'Failed':
    case 'Expired':
      return 'failed';
    default:
      return 'sent';
  }
}

export const useDevicesStore = create<DevicesState>()(
  persist(
    (set, get) => ({
      devicesById: {},
      selectedResidenceId: null,
      selectedResidenceUserId: null,
      hubState: 'disconnected',
      networkOnline: true,
      activeCommandId: null,
      commandPhase: 'idle',
      commandMessage: null,

      setResidence: (residenceId, userId = null) =>
        set({
          selectedResidenceId: residenceId,
          selectedResidenceUserId: residenceId ? userId : null,
        }),

      setDevices: (devices) =>
        set({
          devicesById: Object.fromEntries(devices.map((d) => [d.id, d])),
        }),

      upsertDevice: (device) =>
        set((state) => ({
          devicesById: { ...state.devicesById, [device.id]: device },
        })),

      patchDevice: (deviceId, patch) =>
        set((state) => {
          const current = state.devicesById[deviceId];
          if (!current) return state;
          return {
            devicesById: {
              ...state.devicesById,
              [deviceId]: { ...current, ...patch },
            },
          };
        }),

      setHubState: (hubState) => set({ hubState }),
      setNetworkOnline: (networkOnline) => set({ networkOnline }),

      beginCommand: (command) =>
        set({
          activeCommandId: command.id,
          commandPhase: 'sending',
          commandMessage: 'Comando enviado',
        }),

      applyCommandUpdate: (commandId, status, failureReason) => {
        if (get().activeCommandId && get().activeCommandId !== commandId) {
          return;
        }

        const phase = mapCommandPhase(status);
        set({
          activeCommandId: commandId,
          commandPhase: phase,
          commandMessage:
            phase === 'failed'
              ? failureReason ?? 'Falhou'
              : phase === 'done'
                ? 'Concluído'
                : phase === 'executing'
                  ? 'Executando'
                  : 'Comando enviado',
        });
      },

      resetCommandUi: () =>
        set({ activeCommandId: null, commandPhase: 'idle', commandMessage: null }),

      resetForSessionChange: () =>
        set({
          devicesById: {},
          selectedResidenceId: null,
          selectedResidenceUserId: null,
          hubState: 'disconnected',
          activeCommandId: null,
          commandPhase: 'idle',
          commandMessage: null,
        }),
    }),
    {
      name: 'domus-devices-cache',
      storage: createJSONStorage(() => AsyncStorage),
      partialize: (state) => ({
        devicesById: state.devicesById,
        selectedResidenceId: state.selectedResidenceId,
        selectedResidenceUserId: state.selectedResidenceUserId,
      }),
    },
  ),
);

export function mapGateUiState(
  connectionStatus: string | undefined,
  gateState: GateState | string | null | undefined,
  commandPhase: CommandUiPhase,
  lastAction?: string | null,
): string {
  if (connectionStatus === 'Offline' || connectionStatus === 'Unknown') {
    return 'Offline';
  }

  if (commandPhase === 'sending' || commandPhase === 'sent' || commandPhase === 'executing') {
    if (lastAction === 'Open') return 'Abrindo';
    if (lastAction === 'Close') return 'Fechando';
    if (lastAction === 'Stop') return 'Parado';
  }

  switch (gateState) {
    case 'Open':
      return 'Aberto';
    case 'Closed':
      return 'Fechado';
    case 'Moving':
      return 'Em movimento';
    default:
      return 'Parado';
  }
}

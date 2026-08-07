import * as SignalR from '@microsoft/signalr';
import { env } from '../config/env';
import { getValidAccessToken } from '../api/httpClient';
import { safeLogError } from '../shared/logging';
import type {
  CommandUpdatedEvent,
  DeviceOfflineEvent,
  DeviceStatusChangedEvent,
  GateStateChangedEvent,
  NotificationCreatedEvent,
} from '../shared/types/api';

export type RealtimeHandlers = {
  onDeviceStatusChanged?: (event: DeviceStatusChangedEvent) => void;
  onGateStateChanged?: (event: GateStateChangedEvent) => void;
  onCommandUpdated?: (event: CommandUpdatedEvent) => void;
  onDeviceOffline?: (event: DeviceOfflineEvent) => void;
  onNotificationCreated?: (event: NotificationCreatedEvent) => void;
  onConnectionState?: (state: 'connecting' | 'connected' | 'reconnecting' | 'disconnected') => void;
};

const SCHEMA_VERSION = 1;

function acceptVersion<T extends { schemaVersion?: number }>(payload: T): boolean {
  return (payload.schemaVersion ?? SCHEMA_VERSION) === SCHEMA_VERSION;
}

function isBenignSignalRMessage(message: string): boolean {
  return (
    /stopped during negotiation/i.test(message) ||
    /Failed to start the connection:.*stopped during negotiation/i.test(message) ||
    /Handshake was canceled/i.test(message) ||
    /Invocation canceled/i.test(message)
  );
}

/** Evita console.error do SignalR em cancelamentos esperados (React effect cleanup). */
const quietLogger: SignalR.ILogger = {
  log(logLevel, message) {
    if (isBenignSignalRMessage(message)) {
      return;
    }
    // Só log no Metro — console.warn abre LogBox em tela cheia no aparelho
    if (env.isDev && logLevel >= SignalR.LogLevel.Warning) {
      // eslint-disable-next-line no-console
      console.log(`[SignalR] ${message}`);
    }
  },
};

class DomusSignalRClient {
  private connection: SignalR.HubConnection | null = null;
  private residenceId: string | null = null;
  private tenantId: string | null = null;
  private handlers: RealtimeHandlers = {};
  private connectSeq = 0;
  private inFlight: Promise<void> | null = null;

  setHandlers(handlers: RealtimeHandlers): void {
    this.handlers = handlers;
  }

  /**
   * Conecta (ou reutiliza) o hub. Chamadas concorrentes são serializadas;
   * se já estiver no mesmo residence/tenant, não reinicia.
   */
  async connect(options: { residenceId: string; tenantId: string }): Promise<void> {
    const run = async () => {
      if (
        this.connection?.state === SignalR.HubConnectionState.Connected &&
        this.residenceId === options.residenceId &&
        this.tenantId === options.tenantId
      ) {
        return;
      }

      this.residenceId = options.residenceId;
      this.tenantId = options.tenantId;
      const seq = ++this.connectSeq;

      await this.stopConnection();

      if (seq !== this.connectSeq) {
        return;
      }

      this.handlers.onConnectionState?.('connecting');

      const connection = new SignalR.HubConnectionBuilder()
        .withUrl(env.signalRHubUrl, {
          accessTokenFactory: async () => (await getValidAccessToken()) ?? '',
        })
        .withAutomaticReconnect([1000, 2000, 5000, 10000])
        .configureLogging(quietLogger)
        .build();

      connection.on('DeviceStatusChanged', (payload: DeviceStatusChangedEvent) => {
        if (!acceptVersion(payload)) return;
        this.handlers.onDeviceStatusChanged?.(payload);
      });
      connection.on('GateStateChanged', (payload: GateStateChangedEvent) => {
        if (!acceptVersion(payload)) return;
        this.handlers.onGateStateChanged?.(payload);
      });
      connection.on('CommandUpdated', (payload: CommandUpdatedEvent) => {
        if (!acceptVersion(payload)) return;
        this.handlers.onCommandUpdated?.(payload);
      });
      connection.on('DeviceOffline', (payload: DeviceOfflineEvent) => {
        if (!acceptVersion(payload)) return;
        this.handlers.onDeviceOffline?.(payload);
      });
      connection.on('NotificationCreated', (payload: NotificationCreatedEvent) => {
        if (!acceptVersion(payload)) return;
        this.handlers.onNotificationCreated?.(payload);
      });

      connection.onreconnecting(() => this.handlers.onConnectionState?.('reconnecting'));
      connection.onreconnected(async () => {
        this.handlers.onConnectionState?.('connected');
        await this.joinGroups();
      });
      connection.onclose(() => {
        if (this.connection === connection) {
          this.handlers.onConnectionState?.('disconnected');
        }
      });

      this.connection = connection;

      try {
        await connection.start();
        if (seq !== this.connectSeq || this.connection !== connection) {
          try {
            await connection.stop();
          } catch {
            // ignore
          }
          return;
        }
        this.handlers.onConnectionState?.('connected');
        await this.joinGroups();
      } catch (error) {
        if (isBenignSignalRMessage(error instanceof Error ? error.message : String(error))) {
          if (this.connection === connection) {
            this.handlers.onConnectionState?.('disconnected');
          }
          return;
        }
        if (seq === this.connectSeq) {
          safeLogError('signalr_connect', error);
          this.handlers.onConnectionState?.('disconnected');
        }
      }
    };

    const previous = this.inFlight ?? Promise.resolve();
    this.inFlight = previous.then(run, run);
    await this.inFlight;
  }

  async reconnectWithFreshToken(): Promise<void> {
    if (!this.residenceId || !this.tenantId) {
      return;
    }
    // força novo handshake com JWT atual
    this.connectSeq += 1;
    await this.connect({ residenceId: this.residenceId, tenantId: this.tenantId });
  }

  private async joinGroups(): Promise<void> {
    if (!this.connection || this.connection.state !== SignalR.HubConnectionState.Connected) {
      return;
    }

    try {
      if (this.residenceId) {
        await this.connection.invoke('JoinResidence', this.residenceId);
      }
      if (this.tenantId) {
        await this.connection.invoke('JoinTenant', this.tenantId);
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      if (!isBenignSignalRMessage(message)) {
        safeLogError('signalr_join', error);
      }
    }
  }

  async disconnect(): Promise<void> {
    this.connectSeq += 1;
    await this.stopConnection();
    this.handlers.onConnectionState?.('disconnected');
  }

  private async stopConnection(): Promise<void> {
    const current = this.connection;
    this.connection = null;
    if (!current) {
      return;
    }
    try {
      await current.stop();
    } catch (error) {
      const message = error instanceof Error ? error.message : String(error);
      if (!isBenignSignalRMessage(message)) {
        safeLogError('signalr_stop', error);
      }
    }
  }
}

export const signalRClient = new DomusSignalRClient();

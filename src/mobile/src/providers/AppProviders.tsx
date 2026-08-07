import { PropsWithChildren, useEffect, useRef } from 'react';
import { AppState, type AppStateStatus } from 'react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import NetInfo from '@react-native-community/netinfo';
import { useSessionStore } from '../auth/sessionStore';
import { useDevicesStore } from '../store/devicesStore';
import { registerSessionHooks, getValidAccessToken } from '../api/httpClient';
import { navigateFromNotification } from '../navigation/navigationRef';
import {
  addNotificationReceivedListener,
  addNotificationResponseReceivedListener,
  getLastNotificationResponseAsync,
  syncPushTokenWithBackend,
  unregisterPushTokenFromBackend,
} from '../services/pushNotifications';
import { signalRClient } from '../realtime/signalrClient';
import { AppDialog } from '../ui/AppDialog';

function isGuid(value: string | null | undefined): value is string {
  return !!value && /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
}

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      staleTime: 15_000,
    },
  },
});

function SessionBootstrap({ children }: PropsWithChildren) {
  const hydrate = useSessionStore((s) => s.hydrate);
  const isAuthenticated = useSessionStore((s) => s.isAuthenticated);
  const user = useSessionStore((s) => s.user);
  const clearSession = useSessionStore((s) => s.clearSession);
  const selectedResidenceId = useDevicesStore((s) => s.selectedResidenceId);
  const selectedResidenceUserId = useDevicesStore((s) => s.selectedResidenceUserId);
  const resetForSessionChange = useDevicesStore((s) => s.resetForSessionChange);
  const setHubState = useDevicesStore((s) => s.setHubState);
  const setNetworkOnline = useDevicesStore((s) => s.setNetworkOnline);
  const patchDevice = useDevicesStore((s) => s.patchDevice);
  const applyCommandUpdate = useDevicesStore((s) => s.applyCommandUpdate);
  const lastUserIdRef = useRef<string | null>(null);
  const pendingDisconnectRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    void hydrate();
  }, [hydrate]);

  useEffect(() => {
    registerSessionHooks({
      onSessionExpired: () => {
        void unregisterPushTokenFromBackend().finally(() => {
          void clearSession();
          void signalRClient.disconnect();
        });
      },
      onTokensRefreshed: () => {
        void signalRClient.reconnectWithFreshToken();
      },
    });
  }, [clearSession]);

  useEffect(() => {
    const sub = NetInfo.addEventListener((state) => {
      setNetworkOnline(Boolean(state.isConnected && state.isInternetReachable !== false));
    });
    return () => sub();
  }, [setNetworkOnline]);

  useEffect(() => {
    signalRClient.setHandlers({
      onConnectionState: setHubState,
      onDeviceStatusChanged: (e) => {
        patchDevice(e.deviceId, {
          connectionStatus: e.connectionStatus,
          gateState: e.gateState ?? undefined,
          lastSeenAt: e.reportedAt,
        });
      },
      onGateStateChanged: (e) => {
        patchDevice(e.deviceId, {
          gateState: e.gateState,
          lastSeenAt: e.reportedAt,
          connectionStatus: 'Online',
        });
      },
      onDeviceOffline: (e) => {
        patchDevice(e.deviceId, {
          connectionStatus: 'Offline',
          lastSeenAt: e.lastSeenAt ?? undefined,
        });
      },
      onCommandUpdated: (e) => {
        applyCommandUpdate(e.commandId, e.status, e.failureReason);
      },
      onNotificationCreated: () => {
        void queryClient.invalidateQueries({ queryKey: ['notifications'] });
      },
    });
  }, [applyCommandUpdate, patchDevice, setHubState]);

  useEffect(() => {
    if (!isAuthenticated || !user?.userId) {
      return;
    }

    void syncPushTokenWithBackend();
  }, [isAuthenticated, user?.userId]);

  useEffect(() => {
    let receivedSub: { remove: () => void } | null = null;
    let responseSub: { remove: () => void } | null = null;
    let cancelled = false;

    void (async () => {
      receivedSub = await addNotificationReceivedListener(() => {
        void queryClient.invalidateQueries({ queryKey: ['notifications'] });
      });
      if (cancelled) {
        receivedSub?.remove();
        return;
      }

      responseSub = await addNotificationResponseReceivedListener((response) => {
        const data = response.notification.request.content.data;
        navigateFromNotification(data);
      });
      if (cancelled) {
        responseSub?.remove();
        return;
      }

      const last = await getLastNotificationResponseAsync();
      if (!cancelled && last) {
        navigateFromNotification(last.notification.request.content.data);
      }
    })();

    return () => {
      cancelled = true;
      receivedSub?.remove();
      responseSub?.remove();
    };
  }, []);

  // Reset de store separado — evita disparar disconnect no meio do connect.
  useEffect(() => {
    const nextUserId = user?.userId ?? null;
    if (lastUserIdRef.current === nextUserId) {
      return;
    }
    lastUserIdRef.current = nextUserId;
    resetForSessionChange();
  }, [user?.userId, resetForSessionChange]);

  useEffect(() => {
    if (pendingDisconnectRef.current) {
      clearTimeout(pendingDisconnectRef.current);
      pendingDisconnectRef.current = null;
    }

    if (!isAuthenticated || !user) {
      void signalRClient.disconnect();
      return;
    }

    const selectedForUser =
      selectedResidenceId && selectedResidenceUserId === user.userId
        ? selectedResidenceId
        : null;
    const residenceId = isGuid(selectedForUser)
      ? selectedForUser
      : isGuid(user.residenceId)
        ? user.residenceId
        : null;
    const tenantId = isGuid(user.tenantId) ? user.tenantId : null;
    if (!residenceId || !tenantId) {
      return;
    }

    let cancelled = false;
    const connectTimer = setTimeout(() => {
      void (async () => {
        try {
          await getValidAccessToken();
          if (cancelled) return;
          await signalRClient.connect({ residenceId, tenantId });
        } catch {
          // erros relevantes já são tratados no client
        }
      })();
    }, 200);

    return () => {
      cancelled = true;
      clearTimeout(connectTimer);
      // Atraso evita abortar negotiation no remount (Strict Mode / troca rápida de deps).
      pendingDisconnectRef.current = setTimeout(() => {
        void signalRClient.disconnect();
        pendingDisconnectRef.current = null;
      }, 500);
    };
  }, [
    isAuthenticated,
    user?.userId,
    user?.tenantId,
    user?.residenceId,
    selectedResidenceId,
    selectedResidenceUserId,
  ]);

  useEffect(() => {
    const onChange = (state: AppStateStatus) => {
      if (state === 'active' && isAuthenticated && user?.userId) {
        void getValidAccessToken().then(() => signalRClient.reconnectWithFreshToken());
        void queryClient.invalidateQueries({ queryKey: ['devices'] });
        void queryClient.invalidateQueries({ queryKey: ['residences'] });
        void queryClient.invalidateQueries({ queryKey: ['members'] });
        void queryClient.invalidateQueries({ queryKey: ['history'] });
      }
    };
    const sub = AppState.addEventListener('change', onChange);
    return () => sub.remove();
  }, [isAuthenticated, user?.userId]);

  return <>{children}</>;
}

export function AppProviders({ children }: PropsWithChildren) {
  return (
    <QueryClientProvider client={queryClient}>
      <SessionBootstrap>{children}</SessionBootstrap>
      <AppDialog />
    </QueryClientProvider>
  );
}

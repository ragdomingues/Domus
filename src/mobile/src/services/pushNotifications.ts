import { Platform } from 'react-native';
import Constants from 'expo-constants';
import * as Device from 'expo-device';
import { notificationsApi } from '../api/notificationsApi';
import { safeLogError } from '../shared/logging';

/**
 * Expo Go (SDK 53+) não suporta push remoto no Android — evita crash no import.
 * Builds de loja / EAS (`standalone` / `bare`) seguem o fluxo completo.
 */
export const isExpoGo =
  Constants.appOwnership === 'expo' ||
  Constants.executionEnvironment === 'storeClient';

export const isStoreOrDevClientBuild =
  !isExpoGo &&
  (Constants.executionEnvironment === 'standalone' ||
    Constants.executionEnvironment === 'bare' ||
    Constants.appOwnership == null);

type NotificationsModule = typeof import('expo-notifications');

let notificationsMod: NotificationsModule | null | undefined;
let handlerConfigured = false;

async function getNotifications(): Promise<NotificationsModule | null> {
  if (isExpoGo) {
    return null;
  }

  if (notificationsMod !== undefined) {
    return notificationsMod;
  }

  try {
    const mod = await import('expo-notifications');
    if (!handlerConfigured) {
      mod.setNotificationHandler({
        handleNotification: async () => ({
          shouldShowBanner: true,
          shouldShowList: true,
          shouldPlaySound: true,
          shouldSetBadge: true,
        }),
      });
      handlerConfigured = true;
    }
    notificationsMod = mod;
    return mod;
  } catch (error) {
    safeLogError('push_module_load', error);
    notificationsMod = null;
    return null;
  }
}

let cachedToken: string | null = null;

function getProjectId(): string | undefined {
  return (
    Constants.expoConfig?.extra?.eas?.projectId ??
    Constants.easConfig?.projectId ??
    undefined
  );
}

export async function ensureAndroidNotificationChannel(): Promise<void> {
  const Notifications = await getNotifications();
  if (!Notifications || Platform.OS !== 'android') return;

  // Deve coincidir com ChannelId do backend (domus-alerts) e defaultChannel do plugin
  await Notifications.setNotificationChannelAsync('domus-alerts', {
    name: 'Alertas Domus',
    importance: Notifications.AndroidImportance.MAX,
    vibrationPattern: [0, 250, 150, 250],
    lightColor: '#164A3D',
    sound: 'default',
    lockscreenVisibility: Notifications.AndroidNotificationVisibility.PUBLIC,
    bypassDnd: false,
  });
}

export async function registerForPushNotificationsAsync(): Promise<string | null> {
  try {
    const Notifications = await getNotifications();
    if (!Notifications) {
      return null;
    }

    await ensureAndroidNotificationChannel();

    // Emulador/simulado: token Expo costuma falhar — evita spam de erros
    if (!Device.isDevice) {
      return null;
    }

    const current = await Notifications.getPermissionsAsync();
    let status = current.status;
    if (status !== 'granted') {
      const requested = await Notifications.requestPermissionsAsync();
      status = requested.status;
    }

    if (status !== 'granted') {
      return null;
    }

    const projectId = getProjectId();
    if (!projectId) {
      safeLogError('push_project_id', new Error('EAS projectId ausente em extra.eas.projectId'));
      return null;
    }

    const tokenResult = await Notifications.getExpoPushTokenAsync({ projectId });
    cachedToken = tokenResult.data;
    return cachedToken;
  } catch (error) {
    safeLogError('push_register_local', error);
    return null;
  }
}

export async function syncPushTokenWithBackend(): Promise<void> {
  if (isExpoGo) {
    return;
  }

  const token = cachedToken ?? (await registerForPushNotificationsAsync());
  if (!token) return;

  try {
    await notificationsApi.registerPushToken({
      token,
      platform: Platform.OS === 'ios' ? 'ios' : Platform.OS === 'android' ? 'android' : Platform.OS,
      deviceName: Device.modelName ?? Device.deviceName ?? undefined,
    });
  } catch (error) {
    safeLogError('push_register_backend', error);
  }
}

export async function unregisterPushTokenFromBackend(): Promise<void> {
  const token = cachedToken;
  if (!token) {
    // Tenta obter token em cache local mesmo sem sync recente
    try {
      const Notifications = await getNotifications();
      if (Notifications && Device.isDevice) {
        const projectId = getProjectId();
        if (projectId) {
          const result = await Notifications.getExpoPushTokenAsync({ projectId });
          if (result.data) {
            await notificationsApi.unregisterPushToken(result.data);
          }
        }
      }
    } catch {
      // ignore no logout
    }
    return;
  }

  try {
    await notificationsApi.unregisterPushToken(token);
  } catch (error) {
    safeLogError('push_unregister_backend', error);
  } finally {
    cachedToken = null;
  }
}

export type NotificationSubscription = { remove: () => void };

export async function addNotificationReceivedListener(
  listener: () => void,
): Promise<NotificationSubscription | null> {
  const Notifications = await getNotifications();
  if (!Notifications) return null;
  return Notifications.addNotificationReceivedListener(listener);
}

export async function addNotificationResponseReceivedListener(
  listener: (response: {
    notification: { request: { content: { data: Record<string, unknown> } } };
  }) => void,
): Promise<NotificationSubscription | null> {
  const Notifications = await getNotifications();
  if (!Notifications) return null;
  return Notifications.addNotificationResponseReceivedListener(listener as never);
}

export async function getLastNotificationResponseAsync(): Promise<{
  notification: { request: { content: { data: Record<string, unknown> } } };
} | null> {
  const Notifications = await getNotifications();
  if (!Notifications) return null;
  const response = await Notifications.getLastNotificationResponseAsync();
  return response as never;
}

export function getNotificationDeviceId(data: Record<string, unknown> | undefined): string | null {
  const raw = data?.deviceId;
  return typeof raw === 'string' && raw.length > 0 ? raw : null;
}

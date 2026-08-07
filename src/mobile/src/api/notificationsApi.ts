import { http, normalizeApiError } from './httpClient';
import type {
  DeviceNotificationPreferenceResponse,
  NotificationResponse,
  UpdateDeviceNotificationPreferenceInput,
} from '../shared/types/api';

export const notificationsApi = {
  async getDevicePreferences(deviceId: string): Promise<DeviceNotificationPreferenceResponse> {
    try {
      const { data } = await http.get<DeviceNotificationPreferenceResponse>(
        `/api/devices/${deviceId}/notification-preferences`,
      );
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async updateDevicePreferences(
    deviceId: string,
    input: UpdateDeviceNotificationPreferenceInput,
  ): Promise<DeviceNotificationPreferenceResponse> {
    try {
      const { data } = await http.put<DeviceNotificationPreferenceResponse>(
        `/api/devices/${deviceId}/notification-preferences`,
        input,
      );
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async list(take = 50): Promise<NotificationResponse[]> {
    try {
      const { data } = await http.get<NotificationResponse[]>('/api/notifications', {
        params: { take },
      });
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async markRead(notificationId: string): Promise<void> {
    try {
      await http.post(`/api/notifications/${notificationId}/read`);
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async markAllRead(): Promise<void> {
    try {
      await http.post('/api/notifications/read-all');
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async registerPushToken(input: {
    token: string;
    platform: string;
    deviceName?: string;
  }): Promise<void> {
    try {
      await http.post('/api/me/push-tokens', {
        token: input.token,
        platform: input.platform,
        deviceName: input.deviceName ?? null,
      });
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async unregisterPushToken(token: string): Promise<void> {
    try {
      await http.delete('/api/me/push-tokens', {
        params: { token },
      });
    } catch (error) {
      throw normalizeApiError(error);
    }
  },
};

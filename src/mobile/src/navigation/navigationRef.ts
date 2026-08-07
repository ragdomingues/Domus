import { createNavigationContainerRef } from '@react-navigation/native';
import type { RootStackParamList } from './types';

export const navigationRef = createNavigationContainerRef<RootStackParamList>();

export function navigateFromNotification(data: Record<string, unknown> | undefined): void {
  if (!navigationRef.isReady()) return;

  const deviceId = typeof data?.deviceId === 'string' ? data.deviceId : null;
  if (deviceId) {
    navigationRef.navigate('GateControl', { deviceId });
    return;
  }

  navigationRef.navigate('Notifications');
}

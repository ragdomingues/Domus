import * as ExpoLinking from 'expo-linking';
import { getStateFromPath, type LinkingOptions } from '@react-navigation/native';
import type { RootStackParamList } from './types';

const prefix = ExpoLinking.createURL('/');

/**
 * Deep links:
 * - domus://reset-password?resetToken=...
 * - domus://reset-password?token=... (alias do e-mail web)
 */
export const linking: LinkingOptions<RootStackParamList> = {
  prefixes: [prefix, 'domus://'],
  config: {
    screens: {
      Login: 'login',
      Register: 'register',
      ForgotPassword: 'forgot-password',
      ResetPassword: {
        path: 'reset-password',
        parse: {
          resetToken: (value: string) => value,
          email: (value: string) => value,
        },
      },
      MainTabs: {
        screens: {
          Home: 'home',
          Users: 'users',
        },
      },
      GateControl: 'gate/:deviceId',
      History: 'history/:deviceId',
      Settings: 'settings',
      Notifications: 'notifications',
      ResidenceForm: 'residence',
      DeviceForm: 'device',
    },
  },
  getStateFromPath(path, options) {
    // ?token= do e-mail web → resetToken (params do app)
    const normalized = path.replace(/([?&])token=/g, '$1resetToken=');
    return getStateFromPath(normalized, options);
  },
};

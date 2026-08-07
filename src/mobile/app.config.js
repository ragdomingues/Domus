const fs = require('fs');
const path = require('path');

/** @typedef {'dev' | 'homolog' | 'production'} AppEnvironment */

/**
 * @type {Record<AppEnvironment, { apiBaseUrl: string; signalRHubUrl: string }>}
 */
const ENV_CONFIG = {
  dev: {
    // "auto" → env.ts resolve em runtime (celular = IP LAN, emulador = 10.0.2.2)
    apiBaseUrl: process.env.EXPO_PUBLIC_API_BASE_URL ?? 'http://auto:8080',
    signalRHubUrl:
      process.env.EXPO_PUBLIC_SIGNALR_HUB_URL ?? 'http://auto:8080/hubs/devices',
  },
  homolog: {
    apiBaseUrl: process.env.EXPO_PUBLIC_API_BASE_URL ?? 'https://homolog-api.domus.local',
    signalRHubUrl:
      process.env.EXPO_PUBLIC_SIGNALR_HUB_URL ?? 'https://homolog-api.domus.local/hubs/devices',
  },
  production: {
    apiBaseUrl: process.env.EXPO_PUBLIC_API_BASE_URL ?? 'https://api.domus.app',
    signalRHubUrl: process.env.EXPO_PUBLIC_SIGNALR_HUB_URL ?? 'https://api.domus.app/hubs/devices',
  },
};

/** @returns {AppEnvironment} */
function resolveEnv() {
  const raw = (process.env.EXPO_PUBLIC_APP_ENV ?? 'dev').toLowerCase();
  if (raw === 'homolog' || raw === 'production' || raw === 'dev') {
    return raw;
  }
  return 'dev';
}

/**
 * Resolve google-services.json para builds de loja / EAS.
 * Prioridade: GOOGLE_SERVICES_JSON (path absoluto no CI) → ./google-services.json local.
 */
function resolveGoogleServicesFile() {
  const fromEnv = process.env.GOOGLE_SERVICES_JSON;
  if (fromEnv && fs.existsSync(fromEnv)) {
    return fromEnv;
  }
  const local = path.join(__dirname, 'google-services.json');
  if (fs.existsSync(local)) {
    return './google-services.json';
  }
  return undefined;
}

function resolveIosGoogleServicesFile() {
  const fromEnv = process.env.GOOGLE_SERVICES_PLIST;
  if (fromEnv && fs.existsSync(fromEnv)) {
    return fromEnv;
  }
  const local = path.join(__dirname, 'GoogleService-Info.plist');
  if (fs.existsSync(local)) {
    return './GoogleService-Info.plist';
  }
  return undefined;
}

/** @param {{ config: Record<string, any> }} ctx */
module.exports = ({ config }) => {
  const appEnv = resolveEnv();
  const endpoints = ENV_CONFIG[appEnv];
  const projectId =
    config.extra?.eas?.projectId ?? '177626f0-2648-4d80-8b5e-c9c3118bc7ac';
  const googleServicesFile = resolveGoogleServicesFile();
  const iosGoogleServicesFile = resolveIosGoogleServicesFile();

  return {
    ...config,
    name: appEnv === 'production' ? 'Domus' : `Domus (${appEnv})`,
    slug: 'domus',
    version: '0.3.0',
    orientation: 'portrait',
    icon: './assets/icon.png',
    userInterfaceStyle: 'light',
    scheme: 'domus',
    owner: 'ragdomingues-team',
    // Builds de loja: runtime Version policy para updates/EAS
    runtimeVersion: {
      policy: 'appVersion',
    },
    ios: {
      supportsTablet: true,
      bundleIdentifier: 'app.domus.mobile',
      googleServicesFile: iosGoogleServicesFile,
      infoPlist: {
        UIBackgroundModes: ['remote-notification'],
        NSUserNotificationsUsageDescription:
          'O Domus envia alertas quando o portão abre, fecha ou fica aberto.',
      },
    },
    android: {
      package: 'app.domus.mobile',
      adaptiveIcon: {
        backgroundColor: '#0F2A24',
        foregroundImage: './assets/android-icon-foreground.png',
        backgroundImage: './assets/android-icon-background.png',
        monochromeImage: './assets/android-icon-monochrome.png',
      },
      predictiveBackGestureEnabled: false,
      googleServicesFile,
      permissions: [
        'android.permission.POST_NOTIFICATIONS',
        'android.permission.RECEIVE_BOOT_COMPLETED',
        'android.permission.VIBRATE',
      ],
    },
    web: {
      favicon: './assets/favicon.png',
    },
    plugins: [
      'expo-secure-store',
      'expo-font',
      [
        'expo-notifications',
        {
          icon: './assets/android-icon-monochrome.png',
          color: '#164A3D',
          defaultChannel: 'domus-alerts',
          // Necessário para receber push com app em background/fechado
          enableBackgroundRemoteNotifications: true,
        },
      ],
    ],
    notification: {
      icon: './assets/android-icon-monochrome.png',
      color: '#164A3D',
      androidMode: 'default',
      iosDisplayInForeground: true,
    },
    extra: {
      ...config.extra,
      appEnv,
      apiBaseUrl: endpoints.apiBaseUrl,
      signalRHubUrl: endpoints.signalRHubUrl,
      pushEnabled: Boolean(googleServicesFile) || appEnv !== 'dev',
      eas: {
        ...(config.extra?.eas ?? {}),
        projectId,
      },
    },
  };
};

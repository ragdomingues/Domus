import { Platform } from 'react-native';
import Constants from 'expo-constants';

export type AppEnvironment = 'dev' | 'homolog' | 'production';

type Extra = {
  appEnv?: AppEnvironment;
  apiBaseUrl?: string;
  signalRHubUrl?: string;
};

const extra = (Constants.expoConfig?.extra ?? {}) as Extra;
const appEnv = (extra.appEnv ?? 'dev') as AppEnvironment;

type ConstantsWithLegacy = typeof Constants & {
  manifest2?: { extra?: { expoGo?: { debuggerHost?: string } } };
  manifest?: { debuggerHost?: string; hostUri?: string };
};

/**
 * Host do Metro/Expo no aparelho atual.
 * Celular → IP LAN (ex. 192.168.15.5) | Emulador Android → 10.0.2.2
 */
function resolveDevHost(): string {
  const c = Constants as ConstantsWithLegacy;
  const candidates = [
    Constants.expoConfig?.hostUri,
    c.manifest2?.extra?.expoGo?.debuggerHost,
    c.manifest?.debuggerHost,
    c.manifest?.hostUri,
  ];

  for (const raw of candidates) {
    if (typeof raw !== 'string' || !raw.trim()) continue;
    const hostPort = raw.replace(/^[a-z]+:\/\//i, '').split('/')[0];
    const host = hostPort.split(':')[0]?.trim();
    if (!host) continue;

    if (host === '127.0.0.1' || host === 'localhost') {
      return Platform.OS === 'android' ? '10.0.2.2' : 'localhost';
    }

    return host;
  }

  return Platform.OS === 'android' ? '10.0.2.2' : 'localhost';
}

function buildDevUrl(pathAndPort: string): string {
  return `http://${resolveDevHost()}${pathAndPort}`;
}

function extractPath(url: string | undefined, fallback: string): string {
  if (!url || url === 'auto' || url.includes('://auto')) {
    return fallback;
  }

  try {
    const parsed = new URL(url);
    const port = parsed.port ? `:${parsed.port}` : '';
    return `${port}${parsed.pathname === '/' ? '' : parsed.pathname}`;
  } catch {
    return fallback;
  }
}

const apiBaseUrl =
  appEnv === 'dev'
    ? buildDevUrl(extractPath(extra.apiBaseUrl, ':8080') || ':8080')
    : (extra.apiBaseUrl ?? 'https://api.domus.app');

const signalRHubUrl =
  appEnv === 'dev'
    ? buildDevUrl(extractPath(extra.signalRHubUrl, ':8080/hubs/devices') || ':8080/hubs/devices')
    : (extra.signalRHubUrl ?? 'https://api.domus.app/hubs/devices');

export const env = {
  appEnv,
  apiBaseUrl,
  signalRHubUrl,
  isDev: appEnv === 'dev',
} as const;

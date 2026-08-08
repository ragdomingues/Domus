import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios';
import { env } from '../config/env';
import { secureTokenStore } from '../auth/secureTokenStore';
import { useSessionStore } from '../auth/sessionStore';
import type { ApiErrorBody, AuthTokensResponse } from '../shared/types/api';
import { safeLogError } from '../shared/logging';

type RetryConfig = InternalAxiosRequestConfig & { _retry?: boolean };

let refreshPromise: Promise<string | null> | null = null;
let onSessionExpired: (() => void) | null = null;
let onTokensRefreshed: (() => void) | null = null;

export function registerSessionHooks(hooks: {
  onSessionExpired: () => void;
  onTokensRefreshed?: () => void;
}): void {
  onSessionExpired = hooks.onSessionExpired;
  onTokensRefreshed = hooks.onTokensRefreshed ?? null;
}

export const http = axios.create({
  baseURL: env.apiBaseUrl,
  timeout: 20000,
  headers: {
    Accept: 'application/json',
    'Content-Type': 'application/json',
  },
});

const REFRESH_SKEW_MS = 75_000;

export async function getValidAccessToken(forceRefresh = false): Promise<string | null> {
  const access = await secureTokenStore.getAccessToken();
  const expiresAt = await secureTokenStore.getAccessExpiresAt();

  if (!forceRefresh && access && expiresAt) {
    const ms = Date.parse(expiresAt) - Date.now();
    if (ms > REFRESH_SKEW_MS) {
      return access;
    }
  }

  return refreshAccessToken();
}

async function refreshAccessToken(): Promise<string | null> {
  if (refreshPromise) {
    return refreshPromise;
  }

  refreshPromise = (async () => {
    const refreshToken = await secureTokenStore.getRefreshToken();
    if (!refreshToken) {
      return null;
    }

    try {
      const { data } = await axios.post<AuthTokensResponse>(
        `${env.apiBaseUrl}/api/auth/refresh`,
        { refreshToken },
        { timeout: 20000 },
      );

      await useSessionStore.getState().updateTokens({
        accessToken: data.accessToken,
        refreshToken: data.refreshToken,
        accessTokenExpiresAt: data.accessTokenExpiresAt,
        refreshTokenExpiresAt: data.refreshTokenExpiresAt,
      });

      onTokensRefreshed?.();
      return data.accessToken;
    } catch (error) {
      safeLogError('refresh_failed', error);
      await useSessionStore.getState().clearSession();
      onSessionExpired?.();
      return null;
    } finally {
      refreshPromise = null;
    }
  })();

  return refreshPromise;
}

http.interceptors.request.use(async (config) => {
  const url = config.url ?? '';
  const isAuthRoute =
    url.includes('/api/auth/login') ||
    url.includes('/api/auth/register') ||
    url.includes('/api/auth/refresh') ||
    url.includes('/api/auth/logout');

  if (!isAuthRoute) {
    const token = await getValidAccessToken();
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
  }

  return config;
});

http.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ApiErrorBody>) => {
    const original = error.config as RetryConfig | undefined;
    if (!original || error.response?.status !== 401 || original._retry) {
      return Promise.reject(normalizeApiError(error));
    }

    const url = original.url ?? '';
    if (url.includes('/api/auth/')) {
      return Promise.reject(normalizeApiError(error));
    }

    original._retry = true;
    const token = await refreshAccessToken();
    if (!token) {
      return Promise.reject(normalizeApiError(error));
    }

    original.headers.Authorization = `Bearer ${token}`;
    return http(original);
  },
);

export type AppApiError = Error & {
  code?: string;
  status?: number;
};

export function normalizeApiError(error: unknown): AppApiError {
  if (axios.isAxiosError(error)) {
    const ax = error as AxiosError<ApiErrorBody>;
    const apiMessage = ax.response?.data?.error;
    const isNetwork = ax.code === 'ERR_NETWORK' || ax.message === 'Network Error';
    const message =
      apiMessage ||
      (isNetwork
        ? env.appEnv === 'production'
          ? 'Servidor Domus indisponível no momento. Se o problema continuar, a API de produção ainda não está no ar.'
          : 'Sem conexão com o servidor. Confira a rede e se a API está ligada.'
        : ax.message?.length && ax.message.length < 120
          ? ax.message
          : 'Erro de rede');
    const err = new Error(message) as AppApiError;
    err.code = ax.response?.data?.code ?? ax.code;
    err.status = ax.response?.status;
    return err;
  }

  if (error instanceof Error) {
    const msg = error.message?.length > 160 ? 'Ocorreu um erro inesperado.' : error.message;
    const err = new Error(msg) as AppApiError;
    return err;
  }

  return new Error('Erro desconhecido') as AppApiError;
}

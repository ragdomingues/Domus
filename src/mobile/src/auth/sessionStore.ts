import { create } from 'zustand';
import type { AuthTokensResponse } from '../shared/types/api';
import { secureTokenStore, type PersistedUser, type TokenBundle } from './secureTokenStore';

type SessionState = {
  hydrated: boolean;
  isAuthenticated: boolean;
  user: PersistedUser | null;
  accessToken: string | null;
  accessTokenExpiresAt: string | null;
  bootstrapError: string | null;
  hydrate: () => Promise<void>;
  setSession: (tokens: AuthTokensResponse) => Promise<void>;
  clearSession: () => Promise<void>;
  updateTokens: (partial: {
    accessToken: string;
    refreshToken: string;
    accessTokenExpiresAt: string;
    refreshTokenExpiresAt: string;
  }) => Promise<void>;
  updateUser: (partial: Partial<PersistedUser>) => Promise<void>;
};

function toUser(tokens: AuthTokensResponse): PersistedUser {
  return {
    userId: tokens.userId,
    email: tokens.email,
    name: tokens.name,
    tenantId: tokens.tenantId,
    residenceId: tokens.residenceId,
  };
}

function applyBundle(bundle: TokenBundle) {
  return {
    isAuthenticated: true,
    user: bundle.user,
    accessToken: bundle.accessToken,
    accessTokenExpiresAt: bundle.accessTokenExpiresAt,
  };
}

export const useSessionStore = create<SessionState>((set, get) => ({
  hydrated: false,
  isAuthenticated: false,
  user: null,
  accessToken: null,
  accessTokenExpiresAt: null,
  bootstrapError: null,

  hydrate: async () => {
    try {
      const bundle = await secureTokenStore.load();
      if (!bundle) {
        set({ hydrated: true, isAuthenticated: false, user: null, accessToken: null });
        return;
      }

      const refreshExpired = Date.parse(bundle.refreshTokenExpiresAt) <= Date.now();
      if (refreshExpired) {
        await secureTokenStore.clear();
        set({
          hydrated: true,
          isAuthenticated: false,
          user: null,
          accessToken: null,
          accessTokenExpiresAt: null,
          bootstrapError: 'Sessão expirada. Faça login novamente.',
        });
        return;
      }

      set({ ...applyBundle(bundle), hydrated: true, bootstrapError: null });
    } catch {
      set({
        hydrated: true,
        isAuthenticated: false,
        bootstrapError: 'Não foi possível restaurar a sessão.',
      });
    }
  },

  setSession: async (tokens) => {
    const user = toUser(tokens);
    const bundle: TokenBundle = {
      accessToken: tokens.accessToken,
      refreshToken: tokens.refreshToken,
      accessTokenExpiresAt: tokens.accessTokenExpiresAt,
      refreshTokenExpiresAt: tokens.refreshTokenExpiresAt,
      user,
    };
    await secureTokenStore.save(bundle);
    set({ ...applyBundle(bundle), hydrated: true, bootstrapError: null });
  },

  clearSession: async () => {
    try {
      const { unregisterPushTokenFromBackend } = await import('../services/pushNotifications');
      await unregisterPushTokenFromBackend();
    } catch {
      // best-effort — não bloquear logout
    }
    await secureTokenStore.clear();
    set({
      isAuthenticated: false,
      user: null,
      accessToken: null,
      accessTokenExpiresAt: null,
      bootstrapError: null,
    });
  },

  updateTokens: async (partial) => {
    const current = get();
    if (!current.user) {
      return;
    }

    const refreshToken =
      (await secureTokenStore.getRefreshToken()) ?? partial.refreshToken;

    const bundle: TokenBundle = {
      accessToken: partial.accessToken,
      refreshToken,
      accessTokenExpiresAt: partial.accessTokenExpiresAt,
      refreshTokenExpiresAt: partial.refreshTokenExpiresAt,
      user: current.user,
    };

    // refresh rotaciona — sempre persistir o novo refresh do partial
    bundle.refreshToken = partial.refreshToken;
    await secureTokenStore.save(bundle);
    set({
      accessToken: partial.accessToken,
      accessTokenExpiresAt: partial.accessTokenExpiresAt,
      isAuthenticated: true,
    });
  },

  updateUser: async (partial) => {
    const current = get();
    if (!current.user) {
      return;
    }

    const refreshToken = await secureTokenStore.getRefreshToken();
    if (!refreshToken || !current.accessToken || !current.accessTokenExpiresAt) {
      const user = { ...current.user, ...partial };
      set({ user });
      return;
    }

    const bundle = await secureTokenStore.load();
    if (!bundle) {
      set({ user: { ...current.user, ...partial } });
      return;
    }

    const user = { ...bundle.user, ...partial };
    await secureTokenStore.save({ ...bundle, user });
    set({ user });
  },
}));

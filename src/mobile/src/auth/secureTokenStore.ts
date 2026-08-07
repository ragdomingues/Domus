import * as SecureStore from 'expo-secure-store';

const KEYS = {
  accessToken: 'domus.accessToken',
  refreshToken: 'domus.refreshToken',
  accessExpiresAt: 'domus.accessExpiresAt',
  refreshExpiresAt: 'domus.refreshExpiresAt',
  userJson: 'domus.userJson',
} as const;

export type PersistedUser = {
  userId: string;
  email: string;
  name: string;
  tenantId: string;
  residenceId?: string | null;
};

export type TokenBundle = {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
  user: PersistedUser;
};

async function set(key: string, value: string): Promise<void> {
  await SecureStore.setItemAsync(key, value, {
    keychainAccessible: SecureStore.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
  });
}

async function get(key: string): Promise<string | null> {
  return SecureStore.getItemAsync(key);
}

async function del(key: string): Promise<void> {
  await SecureStore.deleteItemAsync(key);
}

/** Tokens só em SecureStore (nunca AsyncStorage). */
export const secureTokenStore = {
  async save(bundle: TokenBundle): Promise<void> {
    await Promise.all([
      set(KEYS.accessToken, bundle.accessToken),
      set(KEYS.refreshToken, bundle.refreshToken),
      set(KEYS.accessExpiresAt, bundle.accessTokenExpiresAt),
      set(KEYS.refreshExpiresAt, bundle.refreshTokenExpiresAt),
      set(KEYS.userJson, JSON.stringify(bundle.user)),
    ]);
  },

  async load(): Promise<TokenBundle | null> {
    const [accessToken, refreshToken, accessTokenExpiresAt, refreshTokenExpiresAt, userJson] =
      await Promise.all([
        get(KEYS.accessToken),
        get(KEYS.refreshToken),
        get(KEYS.accessExpiresAt),
        get(KEYS.refreshExpiresAt),
        get(KEYS.userJson),
      ]);

    if (!accessToken || !refreshToken || !accessTokenExpiresAt || !refreshTokenExpiresAt || !userJson) {
      return null;
    }

    try {
      const user = JSON.parse(userJson) as PersistedUser;
      return { accessToken, refreshToken, accessTokenExpiresAt, refreshTokenExpiresAt, user };
    } catch {
      await secureTokenStore.clear();
      return null;
    }
  },

  async getAccessToken(): Promise<string | null> {
    return get(KEYS.accessToken);
  },

  async getRefreshToken(): Promise<string | null> {
    return get(KEYS.refreshToken);
  },

  async getAccessExpiresAt(): Promise<string | null> {
    return get(KEYS.accessExpiresAt);
  },

  async clear(): Promise<void> {
    await Promise.all(Object.values(KEYS).map((key) => del(key)));
  },
};

import { http, normalizeApiError } from './httpClient';
import type { AuthTokensResponse } from '../shared/types/api';
import { secureTokenStore } from '../auth/secureTokenStore';
import { env } from '../config/env';
import axios from 'axios';

export type LoginInput = {
  email: string;
  password: string;
};

export type RegisterInput = {
  email: string;
  password: string;
  name: string;
  tenantName: string;
  residenceName?: string;
  timezone?: string;
};

export type ForgotPasswordResponse = {
  message: string;
  resetToken?: string | null;
  expiresAt?: string | null;
};

export type UserProfileResponse = {
  userId: string;
  email: string;
  name: string;
  tenantId?: string | null;
  residenceId?: string | null;
};

export const authApi = {
  async register(input: RegisterInput): Promise<AuthTokensResponse> {
    try {
      const { data } = await http.post<AuthTokensResponse>('/api/auth/register', {
        email: input.email.trim(),
        password: input.password,
        name: input.name.trim(),
        tenantName: input.tenantName.trim(),
        residenceName: input.residenceName?.trim() || undefined,
        timezone: input.timezone?.trim() || 'America/Sao_Paulo',
      });
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async login(input: LoginInput): Promise<AuthTokensResponse> {
    try {
      const { data } = await http.post<AuthTokensResponse>('/api/auth/login', {
        email: input.email.trim(),
        password: input.password,
        deviceInfo: `domus-mobile/${env.appEnv}`,
      });
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async logout(): Promise<void> {
    const refreshToken = await secureTokenStore.getRefreshToken();
    if (!refreshToken) {
      return;
    }

    try {
      await axios.post(
        `${env.apiBaseUrl}/api/auth/logout`,
        { refreshToken },
        { timeout: 15000 },
      );
    } catch {
      // logout local mesmo se API falhar
    }
  },

  async forgotPassword(email: string): Promise<ForgotPasswordResponse> {
    try {
      const { data } = await http.post<ForgotPasswordResponse>('/api/auth/forgot-password', {
        email: email.trim(),
      });
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async resetPassword(token: string, newPassword: string): Promise<void> {
    try {
      await http.post('/api/auth/reset-password', {
        token: token.trim(),
        newPassword,
      });
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async me(): Promise<UserProfileResponse> {
    try {
      const { data } = await http.get<UserProfileResponse>('/api/auth/me');
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async updateProfile(name: string): Promise<UserProfileResponse> {
    try {
      const { data } = await http.patch<UserProfileResponse>('/api/auth/me', {
        name: name.trim(),
      });
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async changePassword(currentPassword: string, newPassword: string): Promise<void> {
    try {
      await http.post('/api/auth/change-password', {
        currentPassword,
        newPassword,
      });
    } catch (error) {
      throw normalizeApiError(error);
    }
  },
};

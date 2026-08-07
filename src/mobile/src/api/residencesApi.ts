import { http, normalizeApiError } from './httpClient';
import type { ResidenceResponse } from '../shared/types/api';

export type CreateResidenceInput = {
  name: string;
  timezone?: string;
  address?: string;
};

export type UpdateResidenceInput = {
  name: string;
  timezone: string;
  address?: string | null;
};

export const residencesApi = {
  async listByTenant(tenantId: string): Promise<ResidenceResponse[]> {
    try {
      const { data } = await http.get<ResidenceResponse[]>(`/api/tenants/${tenantId}/residences`);
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async get(residenceId: string): Promise<ResidenceResponse> {
    try {
      const { data } = await http.get<ResidenceResponse>(`/api/residences/${residenceId}`);
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async create(tenantId: string, input: CreateResidenceInput): Promise<ResidenceResponse> {
    try {
      const { data } = await http.post<ResidenceResponse>(`/api/tenants/${tenantId}/residences`, {
        name: input.name.trim(),
        timezone: input.timezone?.trim() || 'America/Sao_Paulo',
        address: input.address?.trim() || undefined,
      });
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async update(residenceId: string, input: UpdateResidenceInput): Promise<ResidenceResponse> {
    try {
      const { data } = await http.put<ResidenceResponse>(`/api/residences/${residenceId}`, {
        name: input.name.trim(),
        timezone: input.timezone.trim(),
        address: input.address?.trim() || null,
      });
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async remove(residenceId: string): Promise<void> {
    try {
      await http.delete(`/api/residences/${residenceId}`);
    } catch (error) {
      throw normalizeApiError(error);
    }
  },
};

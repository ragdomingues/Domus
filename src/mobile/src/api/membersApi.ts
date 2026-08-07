import { http, normalizeApiError } from './httpClient';
import type { InviteMemberResponse, ResidenceMemberResponse, ResidenceRole } from '../shared/types/api';

export type InviteMemberInput = {
  email: string;
  name?: string;
  role: ResidenceRole;
  validUntilDays?: number;
};

export type UpdateMemberRoleInput = {
  role: ResidenceRole;
  validUntilDays?: number;
};

export const membersApi = {
  async list(residenceId: string): Promise<ResidenceMemberResponse[]> {
    try {
      const { data } = await http.get<ResidenceMemberResponse[]>(
        `/api/residences/${residenceId}/members`,
      );
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async invite(residenceId: string, input: InviteMemberInput): Promise<InviteMemberResponse> {
    try {
      const { data } = await http.post<InviteMemberResponse>(
        `/api/residences/${residenceId}/members`,
        {
          email: input.email.trim(),
          name: input.name?.trim() || undefined,
          role: input.role,
          validUntilDays: input.validUntilDays,
        },
      );
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async updateRole(
    residenceId: string,
    membershipId: string,
    input: UpdateMemberRoleInput,
  ): Promise<ResidenceMemberResponse> {
    try {
      const { data } = await http.patch<ResidenceMemberResponse>(
        `/api/residences/${residenceId}/members/${membershipId}`,
        {
          role: input.role,
          validUntilDays: input.validUntilDays,
        },
      );
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async revoke(residenceId: string, membershipId: string): Promise<void> {
    try {
      await http.delete(`/api/residences/${residenceId}/members/${membershipId}`);
    } catch (error) {
      throw normalizeApiError(error);
    }
  },
};

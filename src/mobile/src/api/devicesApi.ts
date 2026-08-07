import { http, normalizeApiError } from './httpClient';
import type {
  CommandAction,
  CommandResponse,
  DeviceEventResponse,
  DeviceResponse,
  DeviceType,
  DisableSimulationResponse,
  IssueProvisioningResponse,
} from '../shared/types/api';

export type CreateDeviceInput = {
  type: DeviceType;
  name: string;
};

export type UpdateDeviceInput = {
  name: string;
};

export type UpdateDeviceConfigurationInput = {
  relayPulseMs: number;
  heartbeatIntervalSeconds: number;
  commandTimeoutSeconds: number;
  openAlertMinutes?: number | null;
  supportsClose: boolean;
  supportsStop: boolean;
  capabilitiesJson?: string;
};

export const devicesApi = {
  async listByResidence(residenceId: string): Promise<DeviceResponse[]> {
    try {
      const { data } = await http.get<DeviceResponse[]>(`/api/residences/${residenceId}/devices`);
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async get(deviceId: string): Promise<DeviceResponse> {
    try {
      const { data } = await http.get<DeviceResponse>(`/api/devices/${deviceId}`);
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async create(residenceId: string, input: CreateDeviceInput): Promise<DeviceResponse> {
    try {
      const { data } = await http.post<DeviceResponse>(`/api/residences/${residenceId}/devices`, {
        residenceId,
        type: input.type,
        name: input.name.trim(),
      });
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async update(deviceId: string, input: UpdateDeviceInput): Promise<DeviceResponse> {
    try {
      const { data } = await http.put<DeviceResponse>(`/api/devices/${deviceId}`, {
        name: input.name.trim(),
      });
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async updateConfiguration(
    deviceId: string,
    input: UpdateDeviceConfigurationInput,
  ): Promise<DeviceResponse['configuration']> {
    try {
      const { data } = await http.put(`/api/devices/${deviceId}/configuration`, {
        relayPulseMs: input.relayPulseMs,
        heartbeatIntervalSeconds: input.heartbeatIntervalSeconds,
        commandTimeoutSeconds: input.commandTimeoutSeconds,
        openAlertMinutes: input.openAlertMinutes ?? null,
        supportsClose: input.supportsClose,
        supportsStop: input.supportsStop,
        capabilitiesJson: input.capabilitiesJson ?? '{}',
      });
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async remove(deviceId: string): Promise<void> {
    try {
      await http.delete(`/api/devices/${deviceId}`);
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async issueProvisioning(
    deviceId: string,
    expiresInMinutes = 30,
  ): Promise<IssueProvisioningResponse> {
    try {
      const { data } = await http.post<IssueProvisioningResponse>(
        `/api/devices/${deviceId}/provisioning`,
        { expiresInMinutes },
      );
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async enableSimulation(deviceId: string): Promise<DeviceResponse> {
    try {
      const { data } = await http.post<DeviceResponse>(`/api/devices/${deviceId}/simulate`);
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async disableSimulation(
    deviceId: string,
    expiresInMinutes = 30,
  ): Promise<DisableSimulationResponse> {
    try {
      const { data } = await http.delete<DisableSimulationResponse>(
        `/api/devices/${deviceId}/simulate`,
        { params: { expiresInMinutes } },
      );
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async sendCommand(
    deviceId: string,
    action: CommandAction,
    idempotencyKey: string,
  ): Promise<CommandResponse> {
    try {
      const { data } = await http.post<CommandResponse>(`/api/devices/${deviceId}/commands`, {
        action,
        idempotencyKey,
        source: 'MobileApp',
      });
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async listCommands(deviceId: string, take = 50): Promise<CommandResponse[]> {
    try {
      const { data } = await http.get<CommandResponse[]>(`/api/devices/${deviceId}/commands`, {
        params: { take },
      });
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },

  async listEvents(deviceId: string, take = 50): Promise<DeviceEventResponse[]> {
    try {
      const { data } = await http.get<DeviceEventResponse[]>(`/api/devices/${deviceId}/events`, {
        params: { take },
      });
      return data;
    } catch (error) {
      throw normalizeApiError(error);
    }
  },
};

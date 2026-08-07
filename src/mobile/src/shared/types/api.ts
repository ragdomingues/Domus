export type ApiErrorBody = {
  error?: string;
  code?: string;
};

export type AuthTokensResponse = {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
  userId: string;
  email: string;
  name: string;
  tenantId: string;
  residenceId?: string | null;
};

export type ResidenceResponse = {
  id: string;
  tenantId: string;
  name: string;
  timezone: string;
  address?: string | null;
  createdAt: string;
};

export type ResidenceRole = 'Administrator' | 'Member' | 'Visitor';

export type ResidenceMemberResponse = {
  membershipId: string;
  userId: string;
  email: string;
  name: string;
  role: ResidenceRole;
  validFrom: string;
  validUntil?: string | null;
  isActive: boolean;
};

export type InviteMemberResponse = {
  membershipId: string;
  userId: string;
  email: string;
  name: string;
  role: ResidenceRole;
  createdNewUser: boolean;
  temporaryPassword?: string | null;
};

export type DeviceType = 'Gate' | 'Light' | string;
export type DeviceConnectionStatus = 'Unknown' | 'Offline' | 'Online' | string;
export type DeviceLifecycleStatus = 'Created' | 'Provisioning' | 'Active' | 'Suspended' | 'Deleted' | string;
export type GateState = 'Unknown' | 'Closed' | 'Open' | 'Moving' | string;

export type DeviceConfigurationResponse = {
  relayPulseMs: number;
  heartbeatIntervalSeconds: number;
  commandTimeoutSeconds: number;
  openAlertMinutes?: number | null;
  supportsClose: boolean;
  supportsStop: boolean;
  capabilitiesJson: string;
  updatedAt: string;
};

export type DeviceResponse = {
  id: string;
  tenantId: string;
  residenceId: string;
  type: DeviceType;
  name: string;
  lifecycleStatus: DeviceLifecycleStatus;
  connectionStatus: DeviceConnectionStatus;
  firmwareVersion?: string | null;
  hardwareId?: string | null;
  isProvisioned: boolean;
  lastSeenAt?: string | null;
  createdAt: string;
  configuration?: DeviceConfigurationResponse | null;
  gateState?: GateState | null;
  isSimulated?: boolean;
};

export type CommandAction = 'Open' | 'Close' | 'Stop';
export type CommandStatus =
  | 'Pending'
  | 'Sent'
  | 'Delivered'
  | 'Executed'
  | 'Failed'
  | 'Expired'
  | string;

export type CommandSource = 'MobileApp' | 'WebAdmin' | 'Automation' | 'System' | 'API';

export type CommandResponse = {
  id: string;
  tenantId: string;
  deviceId: string;
  userId?: string | null;
  userName?: string | null;
  action: CommandAction | string;
  source: CommandSource | string;
  status: CommandStatus;
  idempotencyKey?: string | null;
  correlationId: string;
  attemptCount: number;
  expiresAt: string;
  sentAt?: string | null;
  deliveredAt?: string | null;
  executedAt?: string | null;
  nextRetryAt?: string | null;
  failureReason?: string | null;
  createdAt: string;
};

export type DeviceEventResponse = {
  id: string;
  deviceId: string;
  userId?: string | null;
  userName?: string | null;
  commandId?: string | null;
  action: string;
  result: string;
  origin: string;
  details?: string | null;
  createdAt: string;
};

export type IssueProvisioningResponse = {
  provisioningId: string;
  deviceId: string;
  provisioningCode: string;
  expiresAt: string;
};

export type DisableSimulationResponse = {
  device: DeviceResponse;
  provisioningId: string;
  provisioningCode: string;
  expiresAt: string;
};

export type RealtimeEnvelope = {
  schemaVersion: number;
};

export type DeviceStatusChangedEvent = RealtimeEnvelope & {
  deviceId: string;
  connectionStatus: string;
  gateState?: string | null;
  reportedAt: string;
};

export type GateStateChangedEvent = RealtimeEnvelope & {
  deviceId: string;
  gateState: string;
  reportedAt: string;
};

export type CommandUpdatedEvent = RealtimeEnvelope & {
  commandId: string;
  deviceId: string;
  status: string;
  action: string;
  failureReason?: string | null;
};

export type DeviceOfflineEvent = RealtimeEnvelope & {
  deviceId: string;
  lastSeenAt?: string | null;
};

export type NotificationCreatedEvent = RealtimeEnvelope & {
  type: string;
  title: string;
  body: string;
  deviceId?: string | null;
  createdAt: string;
};

export type DeviceNotificationPreferenceResponse = {
  deviceId: string;
  notifyOnOpen: boolean;
  notifyOnClose: boolean;
  notifyWhenOpenTooLong: boolean;
  openAlertMinutes: number;
  updatedAt?: string | null;
};

export type UpdateDeviceNotificationPreferenceInput = {
  notifyOnOpen: boolean;
  notifyOnClose: boolean;
  notifyWhenOpenTooLong: boolean;
  openAlertMinutes: number;
};

export type NotificationResponse = {
  id: string;
  type: string;
  title: string;
  body: string;
  payloadJson?: string | null;
  createdAt: string;
  readAt?: string | null;
};

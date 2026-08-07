export type RootStackParamList = {
  Login: undefined;
  Register: undefined;
  ForgotPassword: undefined;
  ResetPassword: { email?: string; resetToken?: string } | undefined;
  MainTabs: undefined;
  GateControl: { deviceId: string };
  History: { deviceId: string };
  Settings: undefined;
  Notifications: undefined;
  ResidenceForm: { residenceId?: string } | undefined;
  DeviceForm: { residenceId: string; deviceId?: string };
};

export type MainTabParamList = {
  Home: undefined;
  Users: undefined;
};

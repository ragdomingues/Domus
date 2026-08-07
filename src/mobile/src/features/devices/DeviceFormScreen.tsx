import { useEffect, useState } from 'react';
import { Pressable, StyleSheet, Switch, Text, View } from 'react-native';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import * as Clipboard from 'expo-clipboard';
import { devicesApi } from '../../api/devicesApi';
import { notificationsApi } from '../../api/notificationsApi';
import type { AppApiError } from '../../api/httpClient';
import type { RootStackParamList } from '../../navigation/types';
import { formatDateTimePtBr } from '../../shared/datetime';
import { colors } from '../../theme/colors';
import { fonts, typography } from '../../theme/typography';
import { radii } from '../../theme/spacing';
import { IconButton } from '../../ui/IconButton';
import { PrimaryButton } from '../../ui/PrimaryButton';
import { Screen } from '../../ui/Screen';
import { TextField } from '../../ui/TextField';
import { appAlert } from '../../ui/dialogStore';

type Props = NativeStackScreenProps<RootStackParamList, 'DeviceForm'>;

export function DeviceFormScreen({ route, navigation }: Props) {
  const { residenceId, deviceId } = route.params;
  const isEdit = !!deviceId;
  const queryClient = useQueryClient();

  const existingQuery = useQuery({
    queryKey: ['device', deviceId],
    enabled: isEdit,
    queryFn: () => devicesApi.get(deviceId!),
  });

  const prefsQuery = useQuery({
    queryKey: ['device-notification-prefs', deviceId],
    enabled: isEdit && existingQuery.data?.type === 'Gate',
    queryFn: () => notificationsApi.getDevicePreferences(deviceId!),
  });

  const [name, setName] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [provisionCode, setProvisionCode] = useState<string | null>(null);
  const [provisionExpires, setProvisionExpires] = useState<string | null>(null);

  const [notifyOnOpen, setNotifyOnOpen] = useState(false);
  const [notifyOnClose, setNotifyOnClose] = useState(false);
  const [notifyWhenOpenTooLong, setNotifyWhenOpenTooLong] = useState(false);
  const [openAlertMinutes, setOpenAlertMinutes] = useState('15');
  const [prefsOk, setPrefsOk] = useState<string | null>(null);
  const [prefsError, setPrefsError] = useState<string | null>(null);

  const [relayPulseMs, setRelayPulseMs] = useState('500');
  const [heartbeatSeconds, setHeartbeatSeconds] = useState('30');
  const [commandTimeoutSeconds, setCommandTimeoutSeconds] = useState('30');
  const [supportsClose, setSupportsClose] = useState(true);
  const [supportsStop, setSupportsStop] = useState(false);
  const [configOk, setConfigOk] = useState<string | null>(null);
  const [configError, setConfigError] = useState<string | null>(null);

  useEffect(() => {
    if (!existingQuery.data) return;
    setName(existingQuery.data.name);
    const cfg = existingQuery.data.configuration;
    if (cfg) {
      setRelayPulseMs(String(cfg.relayPulseMs));
      setHeartbeatSeconds(String(cfg.heartbeatIntervalSeconds));
      setCommandTimeoutSeconds(String(cfg.commandTimeoutSeconds));
      setSupportsClose(cfg.supportsClose);
      setSupportsStop(cfg.supportsStop);
    }
  }, [existingQuery.data]);

  useEffect(() => {
    if (!prefsQuery.data) return;
    setNotifyOnOpen(prefsQuery.data.notifyOnOpen);
    setNotifyOnClose(prefsQuery.data.notifyOnClose);
    setNotifyWhenOpenTooLong(prefsQuery.data.notifyWhenOpenTooLong);
    setOpenAlertMinutes(String(prefsQuery.data.openAlertMinutes ?? 15));
  }, [prefsQuery.data]);

  const saveMutation = useMutation({
    mutationFn: async () => {
      if (isEdit) {
        return devicesApi.update(deviceId!, { name });
      }
      return devicesApi.create(residenceId, { name, type: 'Gate' });
    },
    onSuccess: async (device) => {
      setError(null);
      await queryClient.invalidateQueries({ queryKey: ['devices', residenceId] });
      if (!isEdit) {
        navigation.replace('DeviceForm', { residenceId, deviceId: device.id });
      } else {
        navigation.goBack();
      }
    },
    onError: (err: AppApiError) => setError(err.message || 'Falha ao salvar dispositivo'),
  });

  const prefsMutation = useMutation({
    mutationFn: () => {
      const minutes = Number.parseInt(openAlertMinutes, 10);
      return notificationsApi.updateDevicePreferences(deviceId!, {
        notifyOnOpen,
        notifyOnClose,
        notifyWhenOpenTooLong,
        openAlertMinutes: Number.isFinite(minutes) && minutes > 0 ? minutes : 15,
      });
    },
    onSuccess: async () => {
      setPrefsError(null);
      setPrefsOk('Preferências salvas.');
      await queryClient.invalidateQueries({ queryKey: ['device-notification-prefs', deviceId] });
    },
    onError: (err: AppApiError) => {
      setPrefsOk(null);
      setPrefsError(err.message || 'Falha ao salvar notificações');
    },
  });

  const configMutation = useMutation({
    mutationFn: () => {
      const pulse = Number.parseInt(relayPulseMs, 10);
      const heartbeat = Number.parseInt(heartbeatSeconds, 10);
      const timeout = Number.parseInt(commandTimeoutSeconds, 10);
      return devicesApi.updateConfiguration(deviceId!, {
        relayPulseMs: Number.isFinite(pulse) && pulse > 0 ? pulse : 500,
        heartbeatIntervalSeconds: Number.isFinite(heartbeat) && heartbeat > 0 ? heartbeat : 30,
        commandTimeoutSeconds: Number.isFinite(timeout) && timeout > 0 ? timeout : 30,
        openAlertMinutes: Number.parseInt(openAlertMinutes, 10) || 15,
        supportsClose,
        supportsStop,
      });
    },
    onSuccess: async () => {
      setConfigError(null);
      setConfigOk('Configuração salva e enviada ao aparelho.');
      await queryClient.invalidateQueries({ queryKey: ['device', deviceId] });
      await queryClient.invalidateQueries({ queryKey: ['devices', residenceId] });
    },
    onError: (err: AppApiError) => {
      setConfigOk(null);
      setConfigError(err.message || 'Falha ao salvar configuração');
    },
  });

  const provisionMutation = useMutation({
    mutationFn: () => devicesApi.issueProvisioning(deviceId!, 30),
    onSuccess: (result) => {
      setProvisionCode(result.provisioningCode);
      setProvisionExpires(result.expiresAt);
    },
    onError: (err: AppApiError) => {
      const message =
        err.code === 'device_already_activated'
          ? 'Este aparelho já está ativado. O código de instalação só é necessário na primeira vez.'
          : err.message || 'Não foi possível gerar o código.';
      appAlert('Não foi possível gerar', message);
    },
  });

  const enableDemoMutation = useMutation({
    mutationFn: () => devicesApi.enableSimulation(deviceId!),
    onSuccess: async (device) => {
      setProvisionCode(null);
      setProvisionExpires(null);
      await queryClient.invalidateQueries({ queryKey: ['device', deviceId] });
      await queryClient.invalidateQueries({ queryKey: ['devices', residenceId] });
      appAlert('Modo demonstração', `"${device.name}" está simulado no app, sem aparelho físico.`);
    },
    onError: (err: AppApiError) =>
      appAlert('Erro', err.message || 'Não foi possível ativar o modo demonstração.'),
  });

  const disableDemoMutation = useMutation({
    mutationFn: () => devicesApi.disableSimulation(deviceId!, 30),
    onSuccess: async (result) => {
      setProvisionCode(result.provisioningCode);
      setProvisionExpires(result.expiresAt);
      await queryClient.invalidateQueries({ queryKey: ['device', deviceId] });
      await queryClient.invalidateQueries({ queryKey: ['devices', residenceId] });
      appAlert(
        'Modo demonstração desativado',
        'Use o código de instalação abaixo no aparelho físico.',
      );
    },
    onError: (err: AppApiError) =>
      appAlert('Erro', err.message || 'Não foi possível desativar o modo demonstração.'),
  });

  const deleteMutation = useMutation({
    mutationFn: () => devicesApi.remove(deviceId!),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['devices', residenceId] });
      navigation.goBack();
    },
    onError: (err: AppApiError) => appAlert('Erro', err.message || 'Falha ao remover'),
  });

  const copyCode = async () => {
    if (!provisionCode) return;
    await Clipboard.setStringAsync(provisionCode);
    appAlert('Copiado', 'Código de instalação copiado.');
  };

  const onToggleDemo = (enabled: boolean) => {
    if (enabled) {
      appAlert(
        'Ativar demonstração?',
        'O app vai simular o portão sem aparelho físico. Comandos funcionam só neste modo.',
        [
          { text: 'Cancelar', style: 'cancel' },
          { text: 'Ativar', onPress: () => enableDemoMutation.mutate() },
        ],
      );
      return;
    }

    appAlert(
      'Desativar demonstração?',
      'Vamos gerar um código para você instalar o aparelho físico.',
      [
        { text: 'Cancelar', style: 'cancel' },
        { text: 'Desativar', style: 'destructive', onPress: () => disableDemoMutation.mutate() },
      ],
    );
  };

  const device = existingQuery.data;
  const isGate = (device?.type ?? 'Gate') === 'Gate';
  const isDemo = !!device?.isSimulated;
  const isRealHardware = !!device?.isProvisioned && !isDemo;
  const demoBusy = enableDemoMutation.isPending || disableDemoMutation.isPending;
  const canShowInstallCode = isEdit && !isDemo && !isRealHardware;

  return (
    <Screen scroll>
      <View style={styles.topRow}>
        <IconButton
          name="chevron-back"
          accessibilityLabel="Voltar"
          onPress={() => navigation.goBack()}
        />
        <Text style={styles.topLabel}>Dispositivo</Text>
        <View style={{ width: 44 }} />
      </View>

      <Text style={styles.title}>{isEdit ? 'Gerenciar aparelho' : 'Novo dispositivo'}</Text>
      <Text style={styles.hint}>
        {isEdit
          ? 'Renomeie, configure alertas e o modo demonstração.'
          : 'Cadastre o aparelho e depois gere o código de instalação.'}
      </Text>

      <TextField label="Nome" placeholder="Portão da frente" value={name} onChangeText={setName} />
      {!isEdit ? (
        <Text style={styles.meta}>Tipo: portão (outros aparelhos virão em versões futuras).</Text>
      ) : null}

      {error ? <Text style={styles.error}>{error}</Text> : null}

      <PrimaryButton
        label={isEdit ? 'Salvar nome' : 'Criar dispositivo'}
        variant="brand"
        loading={saveMutation.isPending}
        disabled={!name.trim()}
        onPress={() => saveMutation.mutate()}
        style={{ marginTop: 18 }}
      />

      {isEdit && isGate ? (
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Alertas do portão</Text>
          <Text style={styles.meta}>
            Escolha quando você quer ser avisado. Cada pessoa da casa configura os próprios alertas.
          </Text>

          <View style={styles.toggleRow}>
            <Text style={styles.toggleLabel}>Avisar quando abrir</Text>
            <Switch
              value={notifyOnOpen}
              onValueChange={setNotifyOnOpen}
              trackColor={{ false: colors.border, true: colors.brandMist }}
              thumbColor={notifyOnOpen ? colors.brand : colors.inkFaint}
            />
          </View>
          <View style={styles.toggleRow}>
            <Text style={styles.toggleLabel}>Avisar quando fechar</Text>
            <Switch
              value={notifyOnClose}
              onValueChange={setNotifyOnClose}
              trackColor={{ false: colors.border, true: colors.brandMist }}
              thumbColor={notifyOnClose ? colors.brand : colors.inkFaint}
            />
          </View>
          <View style={styles.toggleRow}>
            <Text style={styles.toggleLabel}>Avisar se ficar aberto</Text>
            <Switch
              value={notifyWhenOpenTooLong}
              onValueChange={setNotifyWhenOpenTooLong}
              trackColor={{ false: colors.border, true: colors.brandMist }}
              thumbColor={notifyWhenOpenTooLong ? colors.brand : colors.inkFaint}
            />
          </View>

          {notifyWhenOpenTooLong ? (
            <TextField
              label="Minutos até o aviso"
              placeholder="15"
              value={openAlertMinutes}
              onChangeText={setOpenAlertMinutes}
              keyboardType="number-pad"
            />
          ) : null}

          {prefsError ? <Text style={styles.error}>{prefsError}</Text> : null}
          {prefsOk ? <Text style={styles.ok}>{prefsOk}</Text> : null}

          <PrimaryButton
            label="Salvar alertas"
            variant="accent"
            loading={prefsMutation.isPending}
            onPress={() => prefsMutation.mutate()}
            style={{ marginTop: 14 }}
          />
        </View>
      ) : null}

      {isEdit && isGate ? (
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Configuração do aparelho</Text>
          <Text style={styles.meta}>
            Ajustes enviados ao portão quando ele estiver online (pulso do relé, tempos e ações
            suportadas).
          </Text>
          <TextField
            label="Pulso do relé (ms)"
            placeholder="500"
            value={relayPulseMs}
            onChangeText={setRelayPulseMs}
            keyboardType="number-pad"
          />
          <TextField
            label="Intervalo de heartbeat (s)"
            placeholder="30"
            value={heartbeatSeconds}
            onChangeText={setHeartbeatSeconds}
            keyboardType="number-pad"
          />
          <TextField
            label="Timeout do comando (s)"
            placeholder="30"
            value={commandTimeoutSeconds}
            onChangeText={setCommandTimeoutSeconds}
            keyboardType="number-pad"
          />
          <View style={styles.toggleRow}>
            <Text style={styles.toggleLabel}>Suporta fechar</Text>
            <Switch
              value={supportsClose}
              onValueChange={setSupportsClose}
              trackColor={{ false: colors.border, true: colors.brandMist }}
              thumbColor={supportsClose ? colors.brand : colors.inkFaint}
            />
          </View>
          <View style={styles.toggleRow}>
            <Text style={styles.toggleLabel}>Suporta parar</Text>
            <Switch
              value={supportsStop}
              onValueChange={setSupportsStop}
              trackColor={{ false: colors.border, true: colors.brandMist }}
              thumbColor={supportsStop ? colors.brand : colors.inkFaint}
            />
          </View>
          {configError ? <Text style={styles.error}>{configError}</Text> : null}
          {configOk ? <Text style={styles.ok}>{configOk}</Text> : null}
          <PrimaryButton
            label="Salvar configuração"
            variant="accent"
            loading={configMutation.isPending}
            onPress={() => configMutation.mutate()}
            style={{ marginTop: 14 }}
          />
        </View>
      ) : null}

      {isEdit && isGate ? (
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Modo demonstração</Text>
          <Text style={styles.meta}>
            Com a demonstração ligada, o app simula o portão sem hardware. Desligue para gerar o
            código de instalação do aparelho físico.
          </Text>
          {isRealHardware ? (
            <>
              <Text style={styles.statusOk}>Aparelho físico ativado</Text>
              <Text style={styles.meta}>
                Este dispositivo já usa hardware real. O modo demonstração não está disponível.
              </Text>
            </>
          ) : (
            <View style={styles.toggleRow}>
              <Text style={styles.toggleLabel}>Usar demonstração</Text>
              <Switch
                value={isDemo}
                disabled={demoBusy}
                onValueChange={onToggleDemo}
                trackColor={{ false: colors.border, true: colors.brandMist }}
                thumbColor={isDemo ? colors.brand : colors.inkFaint}
              />
            </View>
          )}
        </View>
      ) : null}

      {isEdit ? (
        <View style={styles.card}>
          <Text style={styles.cardTitle}>Código de instalação</Text>
          {isDemo ? (
            <Text style={styles.meta}>
              Desative o modo demonstração acima para gerar um código e instalar o aparelho físico.
            </Text>
          ) : isRealHardware ? (
            <>
              <Text style={styles.statusOk}>Instalação concluída</Text>
              <Text style={styles.meta}>
                O código só é usado na primeira ativação do aparelho.
              </Text>
            </>
          ) : (
            <>
              <Text style={styles.meta}>
                Gere um código temporário para ativar este aparelho na residência.
              </Text>
              {!provisionCode ? (
                <PrimaryButton
                  label="Gerar código"
                  variant="accent"
                  loading={provisionMutation.isPending}
                  onPress={() => provisionMutation.mutate()}
                  style={{ marginTop: 14 }}
                />
              ) : null}
            </>
          )}
          {canShowInstallCode && provisionCode ? (
            <Pressable onPress={() => void copyCode()} style={styles.codeBox}>
              <Text style={styles.code}>{provisionCode}</Text>
              <Text style={styles.meta}>
                Expira em {provisionExpires ? formatDateTimePtBr(provisionExpires) : '—'}
                {'\n'}Toque para copiar
              </Text>
            </Pressable>
          ) : null}
        </View>
      ) : null}

      {isEdit ? (
        <PrimaryButton
          label="Remover dispositivo"
          variant="danger"
          loading={deleteMutation.isPending}
          onPress={() =>
            appAlert('Remover', 'Remover este dispositivo?', [
              { text: 'Cancelar', style: 'cancel' },
              { text: 'Remover', style: 'destructive', onPress: () => deleteMutation.mutate() },
            ])
          }
          style={{ marginTop: 12 }}
        />
      ) : null}
    </Screen>
  );
}

const styles = StyleSheet.create({
  topRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 12,
  },
  topLabel: { ...typography.label, color: colors.inkMuted, textTransform: 'uppercase' },
  title: {
    fontFamily: fonts.display,
    fontSize: 30,
    color: colors.brand,
    letterSpacing: -0.6,
  },
  hint: { ...typography.caption, color: colors.inkMuted, marginTop: 6, marginBottom: 8, lineHeight: 18 },
  label: { ...typography.label, color: colors.inkMuted, marginTop: 14, marginBottom: 8 },
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  chip: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radii.pill,
    paddingHorizontal: 14,
    paddingVertical: 9,
    backgroundColor: colors.bgElevated,
  },
  chipActive: { borderColor: colors.brand, backgroundColor: colors.brandMist },
  chipText: { color: colors.inkMuted, fontFamily: fonts.sansSemi },
  chipTextActive: { color: colors.brand },
  error: { color: colors.danger, marginTop: 12, fontFamily: fonts.sansSemi },
  statusOk: {
    marginTop: 10,
    fontFamily: fonts.sansBold,
    fontSize: 16,
    color: colors.brand,
  },

  ok: { color: colors.success, marginTop: 12, fontFamily: fonts.sansSemi },
  card: {
    marginTop: 18,
    backgroundColor: colors.bgElevated,
    borderRadius: radii.lg,
    padding: 16,
    borderWidth: 1,
    borderColor: colors.border,
  },
  cardTitle: { fontFamily: fonts.sansBold, color: colors.brand, fontSize: 16 },
  meta: { ...typography.caption, color: colors.inkMuted, marginTop: 6, lineHeight: 18 },
  toggleRow: {
    marginTop: 14,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: 12,
  },
  toggleLabel: {
    flex: 1,
    fontFamily: fonts.sansSemi,
    color: colors.ink,
    fontSize: 15,
  },
  codeBox: {
    marginTop: 14,
    padding: 14,
    borderRadius: radii.md,
    backgroundColor: colors.brandMist,
  },
  code: {
    fontFamily: fonts.sansBold,
    fontSize: 22,
    letterSpacing: 2,
    color: colors.brand,
    marginBottom: 4,
  },
});

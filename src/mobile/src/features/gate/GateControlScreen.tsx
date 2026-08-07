import { useMemo, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { useMutation, useQuery } from '@tanstack/react-query';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import * as Crypto from 'expo-crypto';
import { Ionicons } from '@expo/vector-icons';
import { devicesApi } from '../../api/devicesApi';
import { mapGateUiState, useDevicesStore } from '../../store/devicesStore';
import type { RootStackParamList } from '../../navigation/types';
import { colors } from '../../theme/colors';
import { fonts, typography } from '../../theme/typography';
import { radii } from '../../theme/spacing';
import { Screen } from '../../ui/Screen';
import { PrimaryButton } from '../../ui/PrimaryButton';
import { StatusBanner } from '../../ui/StatusBanner';
import { IconButton } from '../../ui/IconButton';
import type { AppApiError } from '../../api/httpClient';
import type { CommandAction } from '../../shared/types/api';

type Props = NativeStackScreenProps<RootStackParamList, 'GateControl'>;

export function GateControlScreen({ route, navigation }: Props) {
  const { deviceId } = route.params;
  const device = useDevicesStore((s) => s.devicesById[deviceId]);
  const upsertDevice = useDevicesStore((s) => s.upsertDevice);
  const beginCommand = useDevicesStore((s) => s.beginCommand);
  const applyCommandUpdate = useDevicesStore((s) => s.applyCommandUpdate);
  const patchDevice = useDevicesStore((s) => s.patchDevice);
  const commandPhase = useDevicesStore((s) => s.commandPhase);
  const commandMessage = useDevicesStore((s) => s.commandMessage);
  const hubState = useDevicesStore((s) => s.hubState);
  const networkOnline = useDevicesStore((s) => s.networkOnline);
  const [lastAction, setLastAction] = useState<string | null>(null);
  const [localError, setLocalError] = useState<string | null>(null);

  const deviceQuery = useQuery({
    queryKey: ['device', deviceId],
    queryFn: async () => {
      const d = await devicesApi.get(deviceId);
      upsertDevice(d);
      return d;
    },
  });

  const current = device ?? deviceQuery.data;
  const supportsClose = current?.configuration?.supportsClose ?? true;
  const supportsStop = current?.configuration?.supportsStop ?? false;
  const offline = current?.connectionStatus !== 'Online';
  const needsSetup = !!current && !current.isSimulated && offline;
  const isOpen = current?.gateState === 'Open';
  const primaryAction: CommandAction = isOpen && supportsClose ? 'Close' : 'Open';
  const primaryLabel = primaryAction === 'Close' ? 'Fechar' : 'Abrir';

  const uiState = useMemo(
    () => mapGateUiState(current?.connectionStatus, current?.gateState, commandPhase, lastAction),
    [current?.connectionStatus, current?.gateState, commandPhase, lastAction],
  );

  const commandMutation = useMutation({
    mutationFn: async (action: CommandAction) => {
      const idempotencyKey = await Crypto.randomUUID();
      return devicesApi.sendCommand(deviceId, action, idempotencyKey);
    },
    onMutate: (action) => {
      setLocalError(null);
      setLastAction(action);
    },
    onSuccess: (command) => {
      beginCommand(command);
      applyCommandUpdate(command.id, String(command.status), command.failureReason);
      if (command.status === 'Executed') {
        const nextState =
          command.action === 'Open' ? 'Open' : command.action === 'Close' ? 'Closed' : undefined;
        if (nextState) {
          patchDevice(deviceId, { gateState: nextState, connectionStatus: 'Online' });
        }
      }
      void deviceQuery.refetch();
    },
    onError: (err: AppApiError) => {
      const code = err.code;
      if (code === 'gate_already_open') setLocalError('Portão já está aberto.');
      else if (code === 'gate_already_closed') setLocalError('Portão já está fechado.');
      else if (code === 'command_conflict') setLocalError('Há um comando em andamento.');
      else if (code === 'action_not_supported') setLocalError('Ação não suportada neste dispositivo.');
      else if (code === 'device_not_active') {
        setLocalError(
          'Aparelho ainda não está pronto. Ative a demonstração ou instale o hardware nas configurações.',
        );
      } else setLocalError(err.message || 'Falha ao enviar comando');
    },
  });

  const busy =
    commandMutation.isPending ||
    commandPhase === 'sending' ||
    commandPhase === 'sent' ||
    commandPhase === 'executing';

  const banner = !networkOnline
    ? { tone: 'offline' as const, message: 'Offline' }
    : hubState === 'connecting' || hubState === 'reconnecting'
      ? { tone: 'connecting' as const, message: 'Conectando…' }
      : commandPhase === 'failed'
        ? { tone: 'danger' as const, message: commandMessage ?? 'Falhou' }
        : commandPhase === 'done'
          ? { tone: 'success' as const, message: 'Concluído' }
          : commandPhase === 'executing'
            ? { tone: 'info' as const, message: 'Executando' }
            : commandPhase === 'sent' || commandPhase === 'sending'
              ? { tone: 'info' as const, message: 'Comando enviado' }
              : current?.isSimulated
                ? { tone: 'info' as const, message: 'Modo demonstração (sem aparelho físico)' }
                : null;

  return (
    <View style={{ flex: 1 }}>
      {banner ? <StatusBanner tone={banner.tone} message={banner.message} /> : null}
      <Screen>
        <View style={styles.topRow}>
          <IconButton
            name="chevron-back"
            accessibilityLabel="Voltar"
            onPress={() => navigation.goBack()}
          />
          <Text style={styles.topLabel}>Controle</Text>
          <View style={{ width: 44 }} />
        </View>

        <View style={styles.hero}>
          <View style={styles.heroTop}>
            <View style={styles.heroIcon}>
              <Ionicons name="git-branch-outline" size={32} color={colors.brand} />
            </View>
            {current?.residenceId ? (
              <IconButton
                name="settings-outline"
                accessibilityLabel="Editar dispositivo"
                onPress={() =>
                  navigation.navigate('DeviceForm', {
                    residenceId: current.residenceId,
                    deviceId,
                  })
                }
              />
            ) : null}
          </View>
          <Text style={styles.title}>{current?.name ?? 'Portão'}</Text>
          <Text style={styles.stateLabel}>Estado atual</Text>
          <Text style={styles.stateValue}>{uiState}</Text>
          <View style={styles.statusRow}>
            <View
              style={[
                styles.statusDot,
                {
                  backgroundColor:
                    current?.connectionStatus === 'Online' ? colors.online : colors.offline,
                },
              ]}
            />
            <Text style={styles.meta}>
              {current?.connectionStatus === 'Online'
                ? current.isSimulated
                  ? 'Demonstração online'
                  : 'Dispositivo online'
                : 'Dispositivo offline'}
            </Text>
          </View>

          {needsSetup ? (
            <Text style={styles.setupHint}>
              Para controlar sem hardware, ative o modo demonstração nas configurações. Ou gere o
              código e instale o aparelho físico.
            </Text>
          ) : null}

          {localError ? <Text style={styles.errorInCard}>{localError}</Text> : null}

          <PrimaryButton
            label={primaryLabel}
            variant={primaryAction === 'Open' ? 'accent' : 'brand'}
            loading={commandMutation.isPending && lastAction === primaryAction}
            disabled={busy || offline || !networkOnline}
            onPress={() => commandMutation.mutate(primaryAction)}
            style={styles.bigInCard}
          />
          {supportsStop ? (
            <PrimaryButton
              label="Parar"
              variant="muted"
              loading={commandMutation.isPending && lastAction === 'Stop'}
              disabled={!networkOnline || offline || busy}
              onPress={() => commandMutation.mutate('Stop')}
              style={{ marginTop: 10 }}
            />
          ) : null}

          {needsSetup && current?.residenceId ? (
            <PrimaryButton
              label="Abrir configurações"
              variant="muted"
              onPress={() =>
                navigation.navigate('DeviceForm', {
                  residenceId: current.residenceId,
                  deviceId,
                })
              }
              style={{ marginTop: 10 }}
            />
          ) : null}
        </View>

        <PrimaryButton
          label="Ver histórico"
          variant="muted"
          onPress={() => navigation.navigate('History', { deviceId })}
          style={{ marginTop: 20 }}
        />
      </Screen>
    </View>
  );
}

const styles = StyleSheet.create({
  topRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 20,
  },
  topLabel: { ...typography.label, color: colors.inkMuted, textTransform: 'uppercase' },
  hero: {
    backgroundColor: colors.bgElevated,
    borderRadius: radii.xl,
    padding: 24,
    borderWidth: 1,
    borderColor: colors.border,
    alignItems: 'stretch',
  },
  heroTop: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 16,
  },
  heroIcon: {
    width: 56,
    height: 56,
    borderRadius: radii.lg,
    backgroundColor: colors.brandMist,
    alignItems: 'center',
    justifyContent: 'center',
  },
  title: { fontFamily: fonts.display, fontSize: 32, color: colors.ink, letterSpacing: -0.6 },
  stateLabel: { marginTop: 20, ...typography.label, color: colors.inkMuted },
  stateValue: {
    fontFamily: fonts.display,
    fontSize: 40,
    color: colors.brand,
    marginTop: 4,
    letterSpacing: -1,
  },
  statusRow: { flexDirection: 'row', alignItems: 'center', gap: 8, marginTop: 10 },
  statusDot: { width: 8, height: 8, borderRadius: 4 },
  meta: { ...typography.caption, color: colors.inkMuted },
  setupHint: {
    ...typography.caption,
    color: colors.inkMuted,
    marginTop: 14,
    lineHeight: 18,
  },
  errorInCard: { color: colors.danger, marginTop: 14, fontFamily: fonts.sansSemi },
  bigInCard: { minHeight: 64, marginTop: 22 },
});

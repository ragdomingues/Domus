import { useCallback, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  FlatList,
  Pressable,
  RefreshControl,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import * as Crypto from 'expo-crypto';
import { Ionicons } from '@expo/vector-icons';
import { residencesApi } from '../../api/residencesApi';
import { devicesApi } from '../../api/devicesApi';
import type { AppApiError } from '../../api/httpClient';
import { useSessionStore } from '../../auth/sessionStore';
import { useDevicesStore } from '../../store/devicesStore';
import { colors } from '../../theme/colors';
import { fonts, typography } from '../../theme/typography';
import { radii } from '../../theme/spacing';
import { Screen } from '../../ui/Screen';
import { StatusBanner } from '../../ui/StatusBanner';
import { EmptyState } from '../../ui/EmptyState';
import { AppHeader } from '../../ui/AppHeader';
import { IconButton } from '../../ui/IconButton';
import { authApi } from '../../api/authApi';
import type { RootStackParamList } from '../../navigation/types';
import type { CommandAction, DeviceResponse } from '../../shared/types/api';
import { formatDateTimePtBr } from '../../shared/datetime';
import { appAlert } from '../../ui/dialogStore';
import { unregisterPushTokenFromBackend } from '../../services/pushNotifications';

function formatLastSeen(value?: string | null): string {
  if (!value) return 'Sem comunicação';
  return formatDateTimePtBr(value);
}

function deviceIcon(type: string): keyof typeof Ionicons.glyphMap {
  switch (type) {
    case 'Gate':
      return 'git-branch-outline';
    case 'Light':
      return 'bulb-outline';
    case 'Lock':
      return 'lock-closed-outline';
    case 'Camera':
      return 'videocam-outline';
    default:
      return 'hardware-chip-outline';
  }
}

function gateStateLabel(state?: string | null): string {
  switch (state) {
    case 'Open':
      return 'Aberto';
    case 'Closed':
      return 'Fechado';
    case 'Moving':
      return 'Em movimento';
    default:
      return 'Desconhecido';
  }
}

export function DashboardScreen() {
  const navigation = useNavigation<NativeStackNavigationProp<RootStackParamList>>();
  const queryClient = useQueryClient();
  const user = useSessionStore((s) => s.user);
  const clearSession = useSessionStore((s) => s.clearSession);
  const hubState = useDevicesStore((s) => s.hubState);
  const networkOnline = useDevicesStore((s) => s.networkOnline);
  const devicesById = useDevicesStore((s) => s.devicesById);
  const setDevices = useDevicesStore((s) => s.setDevices);
  const patchDevice = useDevicesStore((s) => s.patchDevice);
  const setResidence = useDevicesStore((s) => s.setResidence);
  const selectedResidenceId = useDevicesStore((s) => s.selectedResidenceId);
  const selectedResidenceUserId = useDevicesStore((s) => s.selectedResidenceUserId);
  const [commandDeviceId, setCommandDeviceId] = useState<string | null>(null);

  const residencesQuery = useQuery({
    queryKey: ['residences', user?.tenantId],
    enabled: !!user?.tenantId,
    queryFn: () => residencesApi.listByTenant(user!.tenantId),
  });

  const selectedForUser =
    selectedResidenceId && selectedResidenceUserId === user?.userId
      ? selectedResidenceId
      : null;

  const activeResidenceId =
    selectedForUser ?? user?.residenceId ?? residencesQuery.data?.[0]?.id ?? null;

  const activeResidenceName =
    residencesQuery.data?.find((r) => r.id === activeResidenceId)?.name ?? 'Residência';

  const devicesQuery = useQuery({
    queryKey: ['devices', activeResidenceId],
    enabled: !!activeResidenceId,
    queryFn: async () => {
      const list = await devicesApi.listByResidence(activeResidenceId!);
      setResidence(activeResidenceId, user?.userId);
      setDevices(list);
      return list;
    },
  });

  const devices = useMemo(() => {
    const fromStore = Object.values(devicesById).filter((d) => d.residenceId === activeResidenceId);
    return fromStore.length > 0 ? fromStore : devicesQuery.data ?? [];
  }, [devicesById, devicesQuery.data, activeResidenceId]);

  const onRefresh = useCallback(() => {
    void residencesQuery.refetch();
    void devicesQuery.refetch();
  }, [residencesQuery, devicesQuery]);

  const gateCommandMutation = useMutation({
    mutationFn: async ({ deviceId, action }: { deviceId: string; action: CommandAction }) => {
      setCommandDeviceId(deviceId);
      const device = devicesById[deviceId];
      if (
        device &&
        !device.isSimulated &&
        (device.connectionStatus !== 'Online' || device.lifecycleStatus !== 'Active')
      ) {
        throw Object.assign(new Error('Ative o modo demonstração ou instale o aparelho nas configurações.'), {
          code: 'needs_setup',
        });
      }
      const idempotencyKey = await Crypto.randomUUID();
      return devicesApi.sendCommand(deviceId, action, idempotencyKey);
    },
    onSuccess: async (command, vars) => {
      const nextState =
        command.action === 'Open' ? 'Open' : command.action === 'Close' ? 'Closed' : undefined;
      if (nextState) {
        patchDevice(vars.deviceId, {
          gateState: nextState,
          connectionStatus: 'Online',
          lastSeenAt: new Date().toISOString(),
        });
      }
      await queryClient.invalidateQueries({ queryKey: ['devices', activeResidenceId] });
      setCommandDeviceId(null);
    },
    onError: (err: AppApiError) => {
      setCommandDeviceId(null);
      appAlert('Portão', err.message || 'Não foi possível enviar o comando');
    },
  });

  const logout = async () => {
    await unregisterPushTokenFromBackend();
    await authApi.logout();
    await clearSession();
  };

  const banner = !networkOnline
    ? { tone: 'offline' as const, message: 'Offline — mostrando último estado conhecido' }
    : hubState === 'connecting' || hubState === 'reconnecting'
      ? { tone: 'connecting' as const, message: 'Conectando…' }
      : hubState === 'disconnected'
        ? { tone: 'offline' as const, message: 'Realtime desconectado' }
        : null;

  const onlineCount = devices.filter((d) => d.connectionStatus === 'Online').length;

  return (
    <View style={{ flex: 1 }}>
      {banner ? <StatusBanner tone={banner.tone} message={banner.message} /> : null}
      <Screen style={{ paddingTop: 8 }} padded>
        <AppHeader
          subtitle={`Olá, ${user?.name?.split(' ')[0] ?? 'morador'}`}
          onSettings={() => navigation.navigate('Settings')}
          onLogout={() => void logout()}
        />

        <View style={styles.summary}>
          <View style={styles.summaryText}>
            <Text style={styles.summaryLabel}>Residência ativa</Text>
            <Text style={styles.summaryTitle}>{activeResidenceName}</Text>
            <Text style={styles.summaryMeta}>
              {devices.length} dispositivo{devices.length === 1 ? '' : 's'}
              {devices.length > 0 ? ` · ${onlineCount} online` : ''}
            </Text>
          </View>
          {activeResidenceId ? (
            <Pressable
              onPress={() => navigation.navigate('ResidenceForm', { residenceId: activeResidenceId })}
              style={styles.summaryBadge}
            >
              <Ionicons name="create-outline" size={22} color={colors.brand} />
            </Pressable>
          ) : (
            <View style={styles.summaryBadge}>
              <Ionicons name="shield-checkmark-outline" size={22} color={colors.brand} />
            </View>
          )}
        </View>

        <View style={styles.sectionRow}>
          <Text style={styles.section}>Residências</Text>
          <Pressable
            accessibilityLabel="Nova residência"
            onPress={() => navigation.navigate('ResidenceForm')}
            style={styles.iconAction}
          >
            <Ionicons name="add" size={20} color={colors.brand} />
          </Pressable>
        </View>
        {residencesQuery.isLoading ? <ActivityIndicator color={colors.brand} /> : null}
        <View style={styles.residenceRow}>
          {(residencesQuery.data ?? []).map((r) => {
            const selected = r.id === activeResidenceId;
            return (
              <Pressable
                key={r.id}
                onPress={() => setResidence(r.id, user?.userId)}
                onLongPress={() => navigation.navigate('ResidenceForm', { residenceId: r.id })}
                style={[styles.residenceChip, selected && styles.residenceChipSelected]}
              >
                <Ionicons
                  name={selected ? 'home' : 'home-outline'}
                  size={14}
                  color={selected ? '#fff' : colors.inkMuted}
                  style={{ marginRight: 6 }}
                />
                <Text style={[styles.residenceText, selected && styles.residenceTextSelected]}>
                  {r.name}
                </Text>
              </Pressable>
            );
          })}
        </View>

        <View style={styles.sectionRow}>
          <Text style={styles.section}>Dispositivos</Text>
          <View style={styles.sectionActions}>
            <Text style={styles.count}>{devices.length}</Text>
            {activeResidenceId ? (
              <Pressable
                accessibilityLabel="Adicionar dispositivo"
                onPress={() => navigation.navigate('DeviceForm', { residenceId: activeResidenceId })}
                style={styles.iconAction}
              >
                <Ionicons name="add" size={20} color={colors.brand} />
              </Pressable>
            ) : null}
          </View>
        </View>

        <FlatList
          data={devices}
          keyExtractor={(item) => item.id}
          contentContainerStyle={{ flexGrow: 1, paddingBottom: 24 }}
          refreshControl={
            <RefreshControl
              refreshing={devicesQuery.isFetching}
              onRefresh={onRefresh}
              tintColor={colors.brand}
            />
          }
          ListEmptyComponent={
            devicesQuery.isLoading ? (
              <ActivityIndicator color={colors.brand} style={{ marginTop: 40 }} />
            ) : (
              <EmptyState
                icon="hardware-chip-outline"
                title="Nenhum dispositivo"
                description="Toque em + para cadastrar um portão e gerar o código de instalação."
              />
            )
          }
          renderItem={({ item }) => (
            <DeviceCard
              device={item}
              busy={commandDeviceId === item.id && gateCommandMutation.isPending}
              networkOnline={networkOnline}
              onOpenHistory={() => {
                if (item.type === 'Gate') {
                  navigation.navigate('History', { deviceId: item.id });
                } else if (activeResidenceId) {
                  navigation.navigate('DeviceForm', {
                    residenceId: activeResidenceId,
                    deviceId: item.id,
                  });
                }
              }}
              onEdit={() => {
                if (!activeResidenceId) return;
                navigation.navigate('DeviceForm', {
                  residenceId: activeResidenceId,
                  deviceId: item.id,
                });
              }}
              onToggleGate={() => {
                const isOpen = item.gateState === 'Open';
                const supportsClose = item.configuration?.supportsClose ?? true;
                const action: CommandAction = isOpen && supportsClose ? 'Close' : 'Open';
                gateCommandMutation.mutate({ deviceId: item.id, action });
              }}
            />
          )}
        />
      </Screen>
    </View>
  );
}

function DeviceCard({
  device,
  busy,
  networkOnline,
  onOpenHistory,
  onEdit,
  onToggleGate,
}: {
  device: DeviceResponse;
  busy: boolean;
  networkOnline: boolean;
  onOpenHistory: () => void;
  onEdit: () => void;
  onToggleGate: () => void;
}) {
  const online = device.connectionStatus === 'Online';
  const isGate = device.type === 'Gate';
  const isOpen = device.gateState === 'Open';
  const supportsClose = device.configuration?.supportsClose ?? true;
  const toggleLabel = isOpen && supportsClose ? 'Fechar' : 'Abrir';

  return (
    <View style={styles.card}>
      <View style={styles.cardHeader}>
        <View style={styles.cardIcon}>
          <Ionicons name={deviceIcon(device.type)} size={22} color={colors.brand} />
        </View>
        <View style={{ flex: 1 }}>
          <Text style={styles.deviceName}>{device.name}</Text>
          <View style={styles.metaRow}>
            <View
              style={[styles.statusPill, { backgroundColor: online ? colors.successSoft : '#E8ECEA' }]}
            >
              <View
                style={[styles.dot, { backgroundColor: online ? colors.online : colors.offline }]}
              />
              <Text style={[styles.statusText, { color: online ? colors.success : colors.inkMuted }]}>
                {online ? 'Online' : 'Offline'}
              </Text>
            </View>
            {isGate ? (
              <Text style={styles.stateChip}>{gateStateLabel(device.gateState)}</Text>
            ) : null}
          </View>
        </View>
        <IconButton name="settings-outline" accessibilityLabel="Editar dispositivo" onPress={onEdit} />
      </View>

      <Text style={styles.meta}>Última comunicação: {formatLastSeen(device.lastSeenAt)}</Text>

      {isGate ? (
        <View style={styles.cardActions}>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={toggleLabel}
            disabled={busy || !networkOnline}
            onPress={onToggleGate}
            style={({ pressed }) => [
              styles.toggleBtn,
              isOpen ? styles.toggleBtnClose : styles.toggleBtnOpen,
              (busy || !networkOnline) && styles.toggleBtnDisabled,
              pressed && !busy && networkOnline && { opacity: 0.9 },
            ]}
          >
            {busy ? (
              <ActivityIndicator color="#fff" />
            ) : (
              <>
                <Ionicons
                  name={isOpen ? 'lock-closed-outline' : 'lock-open-outline'}
                  size={18}
                  color="#fff"
                />
                <Text style={styles.toggleBtnText}>{toggleLabel}</Text>
              </>
            )}
          </Pressable>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Ver histórico"
            onPress={onOpenHistory}
            style={({ pressed }) => [styles.secondaryBtn, pressed && { opacity: 0.85 }]}
          >
            <Text style={styles.secondaryBtnText}>Histórico</Text>
            <Ionicons name="chevron-forward" size={16} color={colors.brand} />
          </Pressable>
        </View>
      ) : (
        <Pressable
          accessibilityRole="button"
          onPress={onEdit}
          style={({ pressed }) => [styles.secondaryBtn, { marginTop: 14 }, pressed && { opacity: 0.85 }]}
        >
          <Text style={styles.secondaryBtnText}>Editar</Text>
          <Ionicons name="chevron-forward" size={16} color={colors.brand} />
        </Pressable>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  summary: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.bgElevated,
    borderRadius: radii.lg,
    padding: 16,
    borderWidth: 1,
    borderColor: colors.border,
    marginBottom: 20,
    gap: 12,
  },
  summaryText: { flex: 1 },
  summaryLabel: {
    ...typography.label,
    color: colors.inkFaint,
    textTransform: 'uppercase',
    letterSpacing: 0.7,
    marginBottom: 4,
  },
  summaryTitle: {
    fontFamily: fonts.display,
    fontSize: 24,
    color: colors.brand,
    letterSpacing: -0.4,
  },
  summaryMeta: { ...typography.caption, color: colors.inkMuted, marginTop: 4 },
  summaryBadge: {
    width: 48,
    height: 48,
    borderRadius: 24,
    backgroundColor: colors.brandMist,
    alignItems: 'center',
    justifyContent: 'center',
  },
  section: {
    ...typography.label,
    color: colors.inkMuted,
    textTransform: 'uppercase',
    letterSpacing: 0.8,
    marginBottom: 10,
    marginTop: 4,
  },
  sectionRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginTop: 8,
    marginBottom: 10,
  },
  sectionActions: { flexDirection: 'row', alignItems: 'center', gap: 10 },
  count: {
    ...typography.caption,
    color: colors.inkFaint,
  },
  iconAction: {
    width: 36,
    height: 36,
    borderRadius: 18,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.bgElevated,
    borderWidth: 1,
    borderColor: colors.border,
  },
  residenceRow: { flexDirection: 'row', flexWrap: 'wrap', gap: 8, marginBottom: 18 },
  residenceChip: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 14,
    paddingVertical: 10,
    borderRadius: radii.pill,
    backgroundColor: colors.bgElevated,
    borderWidth: 1,
    borderColor: colors.border,
  },
  residenceChipSelected: {
    backgroundColor: colors.brand,
    borderColor: colors.brand,
  },
  residenceText: { color: colors.ink, fontFamily: fonts.sansSemi },
  residenceTextSelected: { color: '#fff' },
  card: {
    backgroundColor: colors.bgElevated,
    borderRadius: radii.lg,
    padding: 16,
    marginBottom: 12,
    borderWidth: 1,
    borderColor: colors.border,
    shadowColor: colors.shadow,
    shadowOpacity: 1,
    shadowRadius: 12,
    shadowOffset: { width: 0, height: 6 },
    elevation: 2,
  },
  cardHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
  },
  cardIcon: {
    width: 48,
    height: 48,
    borderRadius: radii.md,
    backgroundColor: colors.brandMist,
    alignItems: 'center',
    justifyContent: 'center',
  },
  deviceName: { fontFamily: fonts.sansBold, fontSize: 17, color: colors.ink },
  metaRow: {
    flexDirection: 'row',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: 8,
    marginTop: 6,
  },
  statusPill: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: radii.pill,
    gap: 5,
  },
  statusText: { fontFamily: fonts.sansSemi, fontSize: 11 },
  stateChip: {
    fontFamily: fonts.sansBold,
    fontSize: 12,
    color: colors.brand,
    backgroundColor: colors.brandMist,
    overflow: 'hidden',
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: radii.pill,
  },
  dot: { width: 7, height: 7, borderRadius: 4 },
  meta: { ...typography.caption, color: colors.inkMuted, marginTop: 12 },
  cardActions: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    marginTop: 14,
  },
  toggleBtn: {
    flex: 1.2,
    minHeight: 48,
    borderRadius: radii.md,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    paddingHorizontal: 14,
  },
  toggleBtnOpen: { backgroundColor: colors.accent },
  toggleBtnClose: { backgroundColor: colors.brand },
  toggleBtnDisabled: { opacity: 0.45 },
  toggleBtnText: { color: '#fff', fontFamily: fonts.sansBold, fontSize: 15 },
  secondaryBtn: {
    flex: 1,
    minHeight: 48,
    borderRadius: radii.md,
    borderWidth: 1.5,
    borderColor: colors.border,
    backgroundColor: colors.bg,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 4,
    paddingHorizontal: 12,
  },
  secondaryBtnText: { color: colors.brand, fontFamily: fonts.sansBold, fontSize: 14 },
});

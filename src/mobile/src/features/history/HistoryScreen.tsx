import { useMemo, useState } from 'react';
import { FlatList, Pressable, StyleSheet, Text, View } from 'react-native';
import { useQuery } from '@tanstack/react-query';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import type { RootStackParamList } from '../../navigation/types';
import { fetchDeviceHistory } from './historyAdapter';
import { formatDateTimePtBr } from '../../shared/datetime';
import { colors } from '../../theme/colors';
import { fonts, typography } from '../../theme/typography';
import { radii } from '../../theme/spacing';
import { Screen } from '../../ui/Screen';
import { IconButton } from '../../ui/IconButton';
import { EmptyState } from '../../ui/EmptyState';

type Props = NativeStackScreenProps<RootStackParamList, 'History'>;

type KindFilter = 'all' | 'command' | 'event';

const KIND_FILTERS: { value: KindFilter; label: string }[] = [
  { value: 'all', label: 'Tudo' },
  { value: 'command', label: 'Comandos' },
  { value: 'event', label: 'Eventos' },
];

export function HistoryScreen({ route, navigation }: Props) {
  const { deviceId } = route.params;
  const [kindFilter, setKindFilter] = useState<KindFilter>('all');

  const historyQuery = useQuery({
    queryKey: ['history', deviceId],
    queryFn: () => fetchDeviceHistory(deviceId),
  });

  const filtered = useMemo(() => {
    const items = historyQuery.data ?? [];
    if (kindFilter === 'all') return items;
    return items.filter((item) => item.kind === kindFilter);
  }, [historyQuery.data, kindFilter]);

  return (
    <Screen>
      <View style={styles.topRow}>
        <IconButton
          name="chevron-back"
          accessibilityLabel="Voltar"
          onPress={() => navigation.goBack()}
        />
        <Text style={styles.topLabel}>Histórico</Text>
        <View style={{ width: 44 }} />
      </View>

      <Text style={styles.title}>Atividade recente</Text>
      <Text style={styles.hint}>Mais recentes primeiro · últimos 90 dias</Text>

      <View style={styles.segment}>
        {KIND_FILTERS.map((f) => {
          const selected = kindFilter === f.value;
          return (
            <Pressable
              key={f.value}
              accessibilityRole="button"
              accessibilityState={{ selected }}
              onPress={() => setKindFilter(f.value)}
              style={[styles.segmentItem, selected && styles.segmentItemSelected]}
            >
              <Text style={[styles.segmentText, selected && styles.segmentTextSelected]}>
                {f.label}
              </Text>
            </Pressable>
          );
        })}
      </View>

      <FlatList
        data={filtered}
        keyExtractor={(item) => item.id}
        refreshing={historyQuery.isFetching}
        onRefresh={() => void historyQuery.refetch()}
        contentContainerStyle={{ flexGrow: 1, paddingTop: 4 }}
        ListEmptyComponent={
          historyQuery.isLoading ? (
            <Text style={styles.empty}>Carregando…</Text>
          ) : (
            <EmptyState
              icon="time-outline"
              title={kindFilter === 'all' ? 'Sem histórico' : 'Nada neste filtro'}
              description={
                kindFilter === 'all'
                  ? 'Os comandos enviados a este dispositivo aparecerão aqui.'
                  : 'Tente ver tudo ou o outro tipo de atividade.'
              }
            />
          )
        }
        renderItem={({ item }) => (
          <View style={styles.card}>
            <View style={styles.cardTop}>
              <Text style={styles.action}>{item.actionLabel}</Text>
              <Text style={styles.kind}>{item.kind === 'event' ? 'Evento' : 'Comando'}</Text>
            </View>
            <Text style={styles.meta}>{item.userLabel}</Text>
            <Text style={styles.meta}>{formatDateTimePtBr(item.createdAt)}</Text>
            <Text style={styles.result}>{item.resultLabel}</Text>
          </View>
        )}
      />
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
    fontSize: 28,
    color: colors.brand,
    letterSpacing: -0.6,
  },
  hint: { ...typography.caption, color: colors.inkMuted, marginTop: 4, marginBottom: 16 },
  segment: {
    flexDirection: 'row',
    backgroundColor: colors.bgElevated,
    borderRadius: radii.md,
    borderWidth: 1,
    borderColor: colors.border,
    padding: 4,
    marginBottom: 16,
  },
  segmentItem: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 10,
    borderRadius: radii.sm,
  },
  segmentItemSelected: {
    backgroundColor: colors.brand,
  },
  segmentText: {
    fontFamily: fonts.sansSemi,
    fontSize: 13,
    color: colors.inkMuted,
  },
  segmentTextSelected: {
    color: '#fff',
  },
  card: {
    backgroundColor: colors.bgElevated,
    borderRadius: radii.md,
    padding: 14,
    borderWidth: 1,
    borderColor: colors.border,
    marginBottom: 10,
  },
  cardTop: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  action: {
    fontFamily: fonts.sansBold,
    color: colors.brand,
    fontSize: 16,
    flex: 1,
    paddingRight: 8,
  },
  kind: { ...typography.caption, color: colors.inkFaint, fontFamily: fonts.sansSemi },
  meta: { ...typography.caption, color: colors.inkMuted, marginTop: 4 },
  result: { marginTop: 8, fontFamily: fonts.sansBold, color: colors.ink },
  empty: {
    textAlign: 'center',
    color: colors.inkMuted,
    marginTop: 40,
    fontFamily: fonts.sansMedium,
  },
});

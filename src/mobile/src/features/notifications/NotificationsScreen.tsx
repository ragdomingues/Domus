import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
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
import { EmptyState } from '../../ui/EmptyState';
import { appAlert } from '../../ui/dialogStore';

type Props = NativeStackScreenProps<RootStackParamList, 'Notifications'>;

export function NotificationsScreen({ navigation }: Props) {
  const queryClient = useQueryClient();

  const listQuery = useQuery({
    queryKey: ['notifications'],
    queryFn: () => notificationsApi.list(50),
  });

  const markAllMutation = useMutation({
    mutationFn: () => notificationsApi.markAllRead(),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['notifications'] });
    },
    onError: (err: AppApiError) => appAlert('Erro', err.message || 'Falha ao marcar como lidas'),
  });

  const markReadMutation = useMutation({
    mutationFn: (id: string) => notificationsApi.markRead(id),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['notifications'] });
    },
  });

  const items = listQuery.data ?? [];
  const unread = items.filter((n) => !n.readAt).length;

  return (
    <Screen scroll>
      <View style={styles.topRow}>
        <IconButton
          name="chevron-back"
          accessibilityLabel="Voltar"
          onPress={() => navigation.goBack()}
        />
        <Text style={styles.topLabel}>Alertas</Text>
        <View style={{ width: 44 }} />
      </View>

      <Text style={styles.title}>Notificações</Text>
      <Text style={styles.hint}>
        {unread > 0 ? `${unread} não lida(s)` : 'Tudo em dia'}
      </Text>

      {unread > 0 ? (
        <PrimaryButton
          label="Marcar todas como lidas"
          variant="accent"
          loading={markAllMutation.isPending}
          onPress={() => markAllMutation.mutate()}
          style={{ marginBottom: 14 }}
        />
      ) : null}

      {listQuery.isLoading ? <Text style={styles.meta}>Carregando…</Text> : null}

      {!listQuery.isLoading && items.length === 0 ? (
        <EmptyState
          title="Nenhum alerta"
          description="Quando o portão abrir, fechar ou ficar aberto tempo demais, os avisos aparecem aqui."
        />
      ) : null}

      {items.map((item) => (
        <Pressable
          key={item.id}
          style={[styles.card, !item.readAt && styles.cardUnread]}
          onPress={() => {
            if (!item.readAt) markReadMutation.mutate(item.id);
          }}
        >
          <Text style={styles.cardTitle}>{item.title}</Text>
          <Text style={styles.body}>{item.body}</Text>
          <Text style={styles.meta}>
            {formatDateTimePtBr(item.createdAt)}
            {!item.readAt ? ' · Nova' : ''}
          </Text>
        </Pressable>
      ))}
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
  hint: { ...typography.caption, color: colors.inkMuted, marginTop: 6, marginBottom: 14 },
  card: {
    backgroundColor: colors.bgElevated,
    borderRadius: radii.lg,
    padding: 16,
    borderWidth: 1,
    borderColor: colors.border,
    marginBottom: 10,
  },
  cardUnread: {
    borderColor: colors.brandSoft,
    backgroundColor: colors.brandMist,
  },
  cardTitle: { fontFamily: fonts.sansBold, color: colors.brand, fontSize: 16 },
  body: { ...typography.body, color: colors.ink, marginTop: 6, lineHeight: 20 },
  meta: { ...typography.caption, color: colors.inkMuted, marginTop: 8 },
});

import { useMemo, useState } from 'react';
import {
  ActivityIndicator,
  Pressable,
  RefreshControl,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { Ionicons } from '@expo/vector-icons';
import { membersApi } from '../../api/membersApi';
import { residencesApi } from '../../api/residencesApi';
import { authApi } from '../../api/authApi';
import type { AppApiError } from '../../api/httpClient';
import { useSessionStore } from '../../auth/sessionStore';
import { useDevicesStore } from '../../store/devicesStore';
import type { RootStackParamList } from '../../navigation/types';
import type { ResidenceMemberResponse, ResidenceRole } from '../../shared/types/api';
import { colors } from '../../theme/colors';
import { fonts, typography } from '../../theme/typography';
import { radii } from '../../theme/spacing';
import { AppHeader } from '../../ui/AppHeader';
import { PrimaryButton } from '../../ui/PrimaryButton';
import { Screen } from '../../ui/Screen';
import { TextField } from '../../ui/TextField';
import { EmptyState } from '../../ui/EmptyState';
import { appAlert } from '../../ui/dialogStore';
import { unregisterPushTokenFromBackend } from '../../services/pushNotifications';

const ROLES: { value: ResidenceRole; label: string }[] = [
  { value: 'Member', label: 'Membro' },
  { value: 'Visitor', label: 'Visitante' },
  { value: 'Administrator', label: 'Admin' },
];

function roleLabel(role: ResidenceRole): string {
  return ROLES.find((r) => r.value === role)?.label ?? role;
}

export function UsersScreen() {
  const navigation = useNavigation<NativeStackNavigationProp<RootStackParamList>>();
  const queryClient = useQueryClient();
  const user = useSessionStore((s) => s.user);
  const clearSession = useSessionStore((s) => s.clearSession);
  const selectedResidenceId = useDevicesStore((s) => s.selectedResidenceId);
  const selectedResidenceUserId = useDevicesStore((s) => s.selectedResidenceUserId);

  const [email, setEmail] = useState('');
  const [name, setName] = useState('');
  const [role, setRole] = useState<ResidenceRole>('Member');
  const [validUntilDays, setValidUntilDays] = useState('7');
  const [formError, setFormError] = useState<string | null>(null);
  const [tempPasswordNotice, setTempPasswordNotice] = useState<string | null>(null);

  const residencesQuery = useQuery({
    queryKey: ['residences', user?.tenantId],
    enabled: !!user?.tenantId,
    queryFn: () => residencesApi.listByTenant(user!.tenantId),
  });

  const selectedForUser =
    selectedResidenceId && selectedResidenceUserId === user?.userId
      ? selectedResidenceId
      : null;

  const residenceId =
    selectedForUser ?? user?.residenceId ?? residencesQuery.data?.[0]?.id ?? null;

  const membersQuery = useQuery({
    queryKey: ['members', residenceId],
    enabled: !!residenceId,
    queryFn: () => membersApi.list(residenceId!),
  });

  const inviteMutation = useMutation({
    mutationFn: () =>
      membersApi.invite(residenceId!, {
        email,
        name: name || undefined,
        role,
        validUntilDays:
          role === 'Visitor' && validUntilDays
            ? Number.parseInt(validUntilDays, 10)
            : undefined,
      }),
    onSuccess: async (result) => {
      setFormError(null);
      setEmail('');
      setName('');
      await queryClient.invalidateQueries({ queryKey: ['members', residenceId] });
      if (result.createdNewUser && result.temporaryPassword) {
        setTempPasswordNotice(
          `Conta criada para ${result.email}. Senha temporária (copie agora): ${result.temporaryPassword}`,
        );
      } else {
        setTempPasswordNotice(`${result.email} foi adicionado à residência.`);
      }
    },
    onError: (err: AppApiError) => {
      setFormError(err.message || 'Falha ao convidar');
    },
  });

  const revokeMutation = useMutation({
    mutationFn: (membershipId: string) => membersApi.revoke(residenceId!, membershipId),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['members', residenceId] });
    },
    onError: (err: AppApiError) => {
      appAlert('Erro', err.message || 'Não foi possível remover o membro');
    },
  });

  const roleMutation = useMutation({
    mutationFn: ({
      membershipId,
      nextRole,
    }: {
      membershipId: string;
      nextRole: ResidenceRole;
    }) =>
      membersApi.updateRole(residenceId!, membershipId, {
        role: nextRole,
        validUntilDays:
          nextRole === 'Visitor' && validUntilDays
            ? Number.parseInt(validUntilDays, 10)
            : undefined,
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['members', residenceId] });
    },
    onError: (err: AppApiError) => {
      appAlert('Erro', err.message || 'Não foi possível alterar o papel');
    },
  });

  const members = useMemo(() => membersQuery.data ?? [], [membersQuery.data]);
  const myMembership = members.find((m) => m.userId === user?.userId && m.isActive);
  const isAdmin = myMembership?.role === 'Administrator';
  const canInvite = isAdmin && !!residenceId && email.trim().length > 3;
  const residenceName =
    residencesQuery.data?.find((r) => r.id === residenceId)?.name ?? 'Residência';

  const logout = async () => {
    await unregisterPushTokenFromBackend();
    await authApi.logout();
    await clearSession();
  };

  const confirmRevoke = (membershipId: string, memberName: string, memberUserId: string) => {
    if (memberUserId === user?.userId) {
      appAlert('Atenção', 'Você não pode remover a si mesmo.');
      return;
    }

    appAlert('Remover acesso', `Revogar acesso de ${memberName}?`, [
      { text: 'Cancelar', style: 'cancel' },
      {
        text: 'Remover',
        style: 'destructive',
        onPress: () => revokeMutation.mutate(membershipId),
      },
    ]);
  };

  const changeRole = (member: ResidenceMemberResponse) => {
    if (!isAdmin || member.userId === user?.userId) return;

    appAlert('Alterar papel', `Novo papel para ${member.name}`, [
      ...ROLES.map((item) => ({
        text: item.label,
        onPress: () =>
          roleMutation.mutate({ membershipId: member.membershipId, nextRole: item.value }),
      })),
      { text: 'Cancelar', style: 'cancel' as const },
    ]);
  };

  if (!residenceId && residencesQuery.isLoading) {
    return (
      <Screen>
        <ActivityIndicator color={colors.brand} />
      </Screen>
    );
  }

  if (!residenceId) {
    return (
      <Screen>
        <AppHeader
          subtitle={`Olá, ${user?.name?.split(' ')[0] ?? 'morador'}`}
          onSettings={() => navigation.navigate('Settings')}
          onLogout={() => void logout()}
        />
        <EmptyState
          icon="people-outline"
          title="Sem residência"
          description="Selecione uma residência na aba inicial para gerenciar usuários."
        />
      </Screen>
    );
  }

  return (
    <Screen
      scroll
      refreshControl={
        <RefreshControl
          refreshing={membersQuery.isFetching}
          onRefresh={() => void membersQuery.refetch()}
          tintColor={colors.brand}
        />
      }
    >
      <AppHeader
        subtitle={`Olá, ${user?.name?.split(' ')[0] ?? 'morador'}`}
        onSettings={() => navigation.navigate('Settings')}
        onLogout={() => void logout()}
      />

      <View style={styles.heroCard}>
        <Text style={styles.heroLabel}>Acessos</Text>
        <Text style={styles.heroTitle}>{residenceName}</Text>
        <Text style={styles.body}>
          {isAdmin
            ? 'Convide moradores ou visitantes e gerencie papéis.'
            : 'Você pode ver os membros desta residência. Apenas administradores convidam.'}
        </Text>
      </View>

      {isAdmin ? (
        <View style={styles.card}>
          <View style={styles.cardHeader}>
            <Ionicons name="person-add-outline" size={18} color={colors.brand} />
            <Text style={styles.cardTitle}>Convidar</Text>
          </View>

          <TextField
            label="E-mail"
            autoCapitalize="none"
            keyboardType="email-address"
            placeholder="pessoa@email.com"
            value={email}
            onChangeText={setEmail}
          />
          <TextField label="Nome (opcional)" placeholder="Nome" value={name} onChangeText={setName} />

          <Text style={styles.label}>Papel</Text>
          <View style={styles.roleRow}>
            {ROLES.map((item) => (
              <Pressable
                key={item.value}
                onPress={() => setRole(item.value)}
                style={[styles.roleChip, role === item.value && styles.roleChipActive]}
              >
                <Text style={[styles.roleChipText, role === item.value && styles.roleChipTextActive]}>
                  {item.label}
                </Text>
              </Pressable>
            ))}
          </View>

          {role === 'Visitor' ? (
            <TextField
              label="Validade (dias)"
              keyboardType="number-pad"
              placeholder="7"
              value={validUntilDays}
              onChangeText={setValidUntilDays}
            />
          ) : null}

          {formError ? <Text style={styles.error}>{formError}</Text> : null}
          {tempPasswordNotice ? (
            <View style={styles.noticeBox}>
              <Text style={styles.notice}>{tempPasswordNotice}</Text>
            </View>
          ) : null}

          <PrimaryButton
            label="Enviar convite"
            variant="brand"
            loading={inviteMutation.isPending}
            disabled={!canInvite}
            onPress={() => inviteMutation.mutate()}
            style={{ marginTop: 16 }}
          />
        </View>
      ) : null}

      <Text style={styles.section}>Membros</Text>
      {membersQuery.isLoading ? (
        <ActivityIndicator color={colors.brand} />
      ) : membersQuery.isError ? (
        <Text style={styles.error}>
          {(membersQuery.error as AppApiError)?.message || 'Falha ao carregar membros'}
        </Text>
      ) : members.length === 0 ? (
        <EmptyState
          icon="people-outline"
          title="Nenhum membro"
          description="Convide alguém para compartilhar o controle da residência."
        />
      ) : (
        members.map((member) => (
          <View key={member.membershipId} style={styles.memberRow}>
            <View style={styles.avatar}>
              <Text style={styles.avatarText}>{member.name.slice(0, 1).toUpperCase()}</Text>
            </View>
            <Pressable
              style={{ flex: 1 }}
              disabled={!isAdmin || member.userId === user?.userId}
              onPress={() => changeRole(member)}
            >
              <Text style={styles.memberName}>{member.name}</Text>
              <Text style={styles.memberMeta}>
                {member.email} · {roleLabel(member.role)}
                {!member.isActive ? ' · inativo' : ''}
                {isAdmin && member.userId !== user?.userId ? ' · tocar p/ papel' : ''}
              </Text>
            </Pressable>
            {isAdmin && member.userId !== user?.userId && member.isActive ? (
              <Pressable
                onPress={() => confirmRevoke(member.membershipId, member.name, member.userId)}
                style={styles.revokeBtn}
              >
                <Ionicons name="trash-outline" size={18} color={colors.danger} />
              </Pressable>
            ) : null}
          </View>
        ))
      )}
    </Screen>
  );
}

const styles = StyleSheet.create({
  heroCard: {
    backgroundColor: colors.bgElevated,
    borderRadius: radii.lg,
    padding: 16,
    borderWidth: 1,
    borderColor: colors.border,
    marginBottom: 18,
  },
  heroLabel: {
    ...typography.label,
    color: colors.inkFaint,
    textTransform: 'uppercase',
    letterSpacing: 0.7,
    marginBottom: 4,
  },
  heroTitle: {
    fontFamily: fonts.display,
    fontSize: 26,
    color: colors.brand,
    letterSpacing: -0.5,
    marginBottom: 6,
  },
  body: { ...typography.caption, color: colors.inkMuted, lineHeight: 20 },
  card: {
    backgroundColor: colors.bgElevated,
    borderRadius: radii.lg,
    padding: 18,
    borderWidth: 1,
    borderColor: colors.border,
    marginBottom: 22,
  },
  cardHeader: { flexDirection: 'row', alignItems: 'center', gap: 8, marginBottom: 4 },
  cardTitle: { fontFamily: fonts.sansBold, color: colors.brand, fontSize: 16 },
  label: { ...typography.label, color: colors.inkMuted, marginBottom: 6, marginTop: 14 },
  roleRow: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  roleChip: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radii.pill,
    paddingHorizontal: 14,
    paddingVertical: 9,
    backgroundColor: colors.bgElevated,
  },
  roleChipActive: { borderColor: colors.brand, backgroundColor: colors.brandMist },
  roleChipText: { color: colors.inkMuted, fontFamily: fonts.sansSemi },
  roleChipTextActive: { color: colors.brand },
  error: { color: colors.danger, marginTop: 12, fontFamily: fonts.sansSemi },
  noticeBox: {
    marginTop: 12,
    backgroundColor: colors.accentSoft,
    borderRadius: radii.md,
    padding: 12,
  },
  notice: { color: colors.ink, fontFamily: fonts.sansSemi, lineHeight: 20 },
  section: {
    ...typography.label,
    color: colors.inkMuted,
    textTransform: 'uppercase',
    letterSpacing: 0.8,
    marginBottom: 12,
  },
  memberRow: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.bgElevated,
    borderRadius: radii.md,
    padding: 14,
    borderWidth: 1,
    borderColor: colors.border,
    marginBottom: 10,
    gap: 12,
  },
  avatar: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: colors.brandMist,
    alignItems: 'center',
    justifyContent: 'center',
  },
  avatarText: { fontFamily: fonts.sansBold, color: colors.brand },
  memberName: { fontFamily: fonts.sansBold, color: colors.ink, fontSize: 16 },
  memberMeta: { ...typography.caption, color: colors.inkMuted, marginTop: 3 },
  revokeBtn: { padding: 8 },
});

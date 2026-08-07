import { useState } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useMutation } from '@tanstack/react-query';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import Constants from 'expo-constants';
import { authApi } from '../../api/authApi';
import type { AppApiError } from '../../api/httpClient';
import { useSessionStore } from '../../auth/sessionStore';
import { env } from '../../config/env';
import type { RootStackParamList } from '../../navigation/types';
import { colors } from '../../theme/colors';
import { fonts, typography } from '../../theme/typography';
import { radii } from '../../theme/spacing';
import { IconButton } from '../../ui/IconButton';
import { PrimaryButton } from '../../ui/PrimaryButton';
import { Screen } from '../../ui/Screen';
import { TextField } from '../../ui/TextField';
import { appAlert } from '../../ui/dialogStore';
import { unregisterPushTokenFromBackend } from '../../services/pushNotifications';

type Props = NativeStackScreenProps<RootStackParamList, 'Settings'>;

export function SettingsScreen({ navigation }: Props) {
  const user = useSessionStore((s) => s.user);
  const updateUser = useSessionStore((s) => s.updateUser);
  const clearSession = useSessionStore((s) => s.clearSession);

  const [name, setName] = useState(user?.name ?? '');
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [profileError, setProfileError] = useState<string | null>(null);
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const [profileOk, setProfileOk] = useState<string | null>(null);
  const [passwordOk, setPasswordOk] = useState<string | null>(null);

  const profileMutation = useMutation({
    mutationFn: () => authApi.updateProfile(name),
    onSuccess: async (profile) => {
      setProfileError(null);
      setProfileOk('Perfil atualizado.');
      await updateUser({ name: profile.name });
    },
    onError: (err: AppApiError) => {
      setProfileOk(null);
      setProfileError(err.message || 'Falha ao atualizar perfil');
    },
  });

  const passwordMutation = useMutation({
    mutationFn: () => authApi.changePassword(currentPassword, newPassword),
    onSuccess: async () => {
      setPasswordError(null);
      setPasswordOk('Senha alterada. Faça login novamente.');
      setCurrentPassword('');
      setNewPassword('');
      await unregisterPushTokenFromBackend();
      await authApi.logout();
      await clearSession();
    },
    onError: (err: AppApiError) => {
      setPasswordOk(null);
      setPasswordError(err.message || 'Falha ao alterar senha');
    },
  });

  const logout = () => {
    appAlert('Sair', 'Deseja encerrar a sessão?', [
      { text: 'Cancelar', style: 'cancel' },
      {
        text: 'Sair',
        style: 'destructive',
        onPress: () => {
          void (async () => {
            await unregisterPushTokenFromBackend();
            await authApi.logout();
            await clearSession();
          })();
        },
      },
    ]);
  };

  return (
    <Screen scroll>
      <View style={styles.topRow}>
        <IconButton
          name="chevron-back"
          accessibilityLabel="Voltar"
          onPress={() => navigation.goBack()}
        />
        <Text style={styles.topLabel}>Conta</Text>
        <View style={{ width: 44 }} />
      </View>

      <Text style={styles.title}>Configurações</Text>
      <Text style={styles.hint}>{user?.email}</Text>

      <Pressable style={styles.card} onPress={() => navigation.navigate('Notifications')}>
        <Text style={styles.cardTitle}>Notificações</Text>
        <Text style={styles.meta}>Ver alertas do portão e marcar como lidos.</Text>
      </Pressable>

      <View style={styles.card}>
        <Text style={styles.cardTitle}>Perfil</Text>
        <TextField label="Nome" value={name} onChangeText={setName} placeholder="Seu nome" />
        {profileError ? <Text style={styles.error}>{profileError}</Text> : null}
        {profileOk ? <Text style={styles.ok}>{profileOk}</Text> : null}
        <PrimaryButton
          label="Salvar perfil"
          variant="brand"
          loading={profileMutation.isPending}
          disabled={!name.trim() || name.trim() === user?.name}
          onPress={() => profileMutation.mutate()}
          style={{ marginTop: 16 }}
        />
      </View>

      <View style={styles.card}>
        <Text style={styles.cardTitle}>Alterar senha</Text>
        <TextField
          label="Senha atual"
          secureTextEntry
          value={currentPassword}
          onChangeText={setCurrentPassword}
          placeholder="••••••••"
        />
        <TextField
          label="Nova senha (mín. 8)"
          secureTextEntry
          value={newPassword}
          onChangeText={setNewPassword}
          placeholder="••••••••"
        />
        {passwordError ? <Text style={styles.error}>{passwordError}</Text> : null}
        {passwordOk ? <Text style={styles.ok}>{passwordOk}</Text> : null}
        <PrimaryButton
          label="Atualizar senha"
          variant="brand"
          loading={passwordMutation.isPending}
          disabled={!currentPassword || newPassword.length < 8}
          onPress={() => passwordMutation.mutate()}
          style={{ marginTop: 16 }}
        />
      </View>

      <View style={styles.card}>
        <Text style={styles.cardTitle}>Sobre</Text>
        <Text style={styles.meta}>Domus · {Constants.expoConfig?.version ?? '0.3.0'}</Text>
        <Text style={styles.meta}>Ambiente: {env.appEnv}</Text>
        <Text style={styles.meta}>API: {env.apiBaseUrl}</Text>
      </View>

      <Pressable onPress={logout} style={styles.logout}>
        <Text style={styles.logoutText}>Sair da conta</Text>
      </Pressable>
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
    fontSize: 32,
    color: colors.brand,
    letterSpacing: -0.8,
  },
  hint: { ...typography.caption, color: colors.inkMuted, marginTop: 4, marginBottom: 18 },
  card: {
    backgroundColor: colors.bgElevated,
    borderRadius: radii.lg,
    padding: 16,
    borderWidth: 1,
    borderColor: colors.border,
    marginBottom: 14,
  },
  cardTitle: { fontFamily: fonts.sansBold, color: colors.brand, fontSize: 16 },
  error: { color: colors.danger, marginTop: 12, fontFamily: fonts.sansSemi },
  ok: { color: colors.success, marginTop: 12, fontFamily: fonts.sansSemi },
  meta: { ...typography.caption, color: colors.inkMuted, marginTop: 8 },
  logout: {
    marginTop: 8,
    marginBottom: 24,
    alignItems: 'center',
    paddingVertical: 14,
  },
  logoutText: { color: colors.danger, fontFamily: fonts.sansBold, fontSize: 15 },
});

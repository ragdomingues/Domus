import { useEffect, useState } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  Pressable,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { useMutation } from '@tanstack/react-query';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import { authApi } from '../../api/authApi';
import type { AppApiError } from '../../api/httpClient';
import type { RootStackParamList } from '../../navigation/types';
import { colors } from '../../theme/colors';
import { fonts, typography } from '../../theme/typography';
import { radii } from '../../theme/spacing';
import { BrandMark } from '../../ui/BrandMark';
import { AuthSheet } from '../../ui/AuthSheet';
import { PrimaryButton } from '../../ui/PrimaryButton';
import { Screen } from '../../ui/Screen';
import { TextField } from '../../ui/TextField';
import { IconButton } from '../../ui/IconButton';

type Props = NativeStackScreenProps<RootStackParamList, 'ResetPassword'>;

export function ResetPasswordScreen({ route, navigation }: Props) {
  const email = route.params?.email;
  const [token, setToken] = useState(route.params?.resetToken ?? '');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (route.params?.resetToken) {
      setToken(route.params.resetToken);
    }
  }, [route.params?.resetToken]);

  const mutation = useMutation({
    mutationFn: () => authApi.resetPassword(token, password),
    onSuccess: () => {
      setError(null);
      navigation.reset({
        index: 0,
        routes: [{ name: 'Login' }],
      });
    },
    onError: (err: AppApiError) => {
      setError(err.message || 'Não foi possível redefinir a senha');
    },
  });

  const canSubmit =
    token.trim().length > 8 && password.length >= 8 && password === confirm;

  return (
    <Screen variant="auth" padded={false} scroll>
      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        style={styles.root}
      >
        <View style={styles.hero}>
          <View style={styles.back}>
            <IconButton
              name="chevron-back"
              tone="onDark"
              accessibilityLabel="Voltar"
              onPress={() => navigation.goBack()}
            />
          </View>
          <BrandMark
            compact
            tone="onDark"
            subtitle={
              email
                ? `Enviamos um código para ${email}`
                : 'Cole o código do e-mail e escolha uma nova senha'
            }
          />
        </View>

        <AuthSheet>
          <Text style={styles.formTitle}>Nova senha</Text>

          {email ? (
            <View style={styles.notice}>
              <Text style={styles.noticeText}>
                Abra o e-mail, copie o código de redefinição e cole abaixo. Depois defina a nova
                senha.
              </Text>
            </View>
          ) : null}

          <TextField
            label="Código do e-mail"
            autoCapitalize="none"
            autoCorrect={false}
            placeholder="Cole o código recebido"
            value={token}
            onChangeText={setToken}
          />

          <TextField
            label="Nova senha (mín. 8)"
            secureTextEntry
            placeholder="••••••••"
            value={password}
            onChangeText={setPassword}
          />
          <TextField
            label="Confirmar senha"
            secureTextEntry
            placeholder="••••••••"
            value={confirm}
            onChangeText={setConfirm}
          />

          {password && confirm && password !== confirm ? (
            <Text style={styles.error}>As senhas não coincidem</Text>
          ) : null}
          {error ? <Text style={styles.error}>{error}</Text> : null}

          <PrimaryButton
            label="Redefinir senha"
            variant="brand"
            loading={mutation.isPending}
            disabled={!canSubmit}
            onPress={() => mutation.mutate()}
            style={{ marginTop: 18 }}
          />

          <Pressable onPress={() => navigation.navigate('Login')} style={styles.linkWrap}>
            <Text style={styles.link}>Voltar ao login</Text>
          </Pressable>
        </AuthSheet>
      </KeyboardAvoidingView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  root: { flexGrow: 1, justifyContent: 'flex-end' },
  hero: {
    minHeight: 220,
    justifyContent: 'center',
    paddingHorizontal: 24,
    paddingTop: 24,
  },
  back: { position: 'absolute', top: 8, left: 16, zIndex: 2 },
  formTitle: {
    fontFamily: fonts.display,
    fontSize: 28,
    color: colors.ink,
    letterSpacing: -0.5,
    marginBottom: 4,
  },
  notice: {
    marginTop: 8,
    marginBottom: 4,
    padding: 12,
    borderRadius: radii.md,
    backgroundColor: colors.brandMist,
  },
  noticeText: { ...typography.caption, color: colors.brandSoft, lineHeight: 18 },
  error: { color: colors.danger, marginTop: 12, fontFamily: fonts.sansSemi },
  linkWrap: { marginTop: 20, alignItems: 'center' },
  link: { ...typography.caption, color: colors.brand, fontFamily: fonts.sansBold },
});

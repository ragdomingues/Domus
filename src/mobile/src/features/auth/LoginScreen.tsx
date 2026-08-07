import { useState } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  Pressable,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import { useMutation } from '@tanstack/react-query';
import { useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { authApi } from '../../api/authApi';
import { useSessionStore } from '../../auth/sessionStore';
import type { RootStackParamList } from '../../navigation/types';
import { colors } from '../../theme/colors';
import { fonts, typography } from '../../theme/typography';
import { BrandMark } from '../../ui/BrandMark';
import { AuthSheet } from '../../ui/AuthSheet';
import { PrimaryButton } from '../../ui/PrimaryButton';
import { Screen } from '../../ui/Screen';
import { TextField } from '../../ui/TextField';
import type { AppApiError } from '../../api/httpClient';

export function LoginScreen() {
  const navigation = useNavigation<NativeStackNavigationProp<RootStackParamList>>();
  const setSession = useSessionStore((s) => s.setSession);
  const bootstrapError = useSessionStore((s) => s.bootstrapError);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(bootstrapError);

  const loginMutation = useMutation({
    mutationFn: () => authApi.login({ email, password }),
    onSuccess: async (tokens) => {
      setError(null);
      await setSession(tokens);
    },
    onError: (err: AppApiError) => {
      setError(err.message || 'Falha no login');
    },
  });

  return (
    <Screen variant="auth" padded={false}>
      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        style={styles.root}
      >
        <View style={styles.hero}>
          <BrandMark tone="onDark" subtitle="Controle seguro da sua residência" />
        </View>

        <AuthSheet>
          <Text style={styles.formTitle}>Bem-vindo de volta</Text>
          <Text style={styles.formHint}>Entre para gerenciar portões e acessos</Text>

          <TextField
            label="E-mail"
            autoCapitalize="none"
            autoCorrect={false}
            keyboardType="email-address"
            placeholder="voce@email.com"
            value={email}
            onChangeText={setEmail}
          />
          <TextField
            label="Senha"
            secureTextEntry
            placeholder="••••••••"
            value={password}
            onChangeText={setPassword}
          />

          {error ? <Text style={styles.error}>{error}</Text> : null}

          <Pressable
            onPress={() => navigation.navigate('ForgotPassword')}
            style={styles.forgotWrap}
          >
            <Text style={styles.forgot}>Esqueci a senha</Text>
          </Pressable>

          <PrimaryButton
            label="Entrar na residência"
            variant="brand"
            loading={loginMutation.isPending}
            disabled={!email || !password}
            onPress={() => loginMutation.mutate()}
            style={{ marginTop: 12 }}
          />

          <Pressable onPress={() => navigation.navigate('Register')} style={styles.linkWrap}>
            <Text style={styles.linkMuted}>Novo por aqui?</Text>
            <Text style={styles.link}> Criar conta</Text>
          </Pressable>
        </AuthSheet>
      </KeyboardAvoidingView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  root: { flex: 1, justifyContent: 'flex-end' },
  hero: {
    flex: 1,
    justifyContent: 'center',
    paddingHorizontal: 24,
    paddingBottom: 8,
  },
  formTitle: {
    fontFamily: fonts.display,
    fontSize: 28,
    letterSpacing: -0.5,
    color: colors.ink,
    marginBottom: 6,
  },
  formHint: { ...typography.caption, color: colors.inkMuted, marginBottom: 6, lineHeight: 18 },
  error: { color: colors.danger, marginTop: 12, fontFamily: fonts.sansSemi },
  forgotWrap: { alignSelf: 'flex-end', marginTop: 14 },
  forgot: { ...typography.caption, color: colors.brandSoft, fontFamily: fonts.sansSemi },
  linkWrap: {
    marginTop: 22,
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
  },
  linkMuted: { ...typography.caption, color: colors.inkMuted },
  link: { ...typography.caption, color: colors.brand, fontFamily: fonts.sansBold },
});

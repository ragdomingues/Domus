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

export function RegisterScreen() {
  const navigation = useNavigation<NativeStackNavigationProp<RootStackParamList>>();
  const setSession = useSessionStore((s) => s.setSession);
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [tenantName, setTenantName] = useState('');
  const [residenceName, setResidenceName] = useState('');
  const [error, setError] = useState<string | null>(null);

  const registerMutation = useMutation({
    mutationFn: () =>
      authApi.register({
        name,
        email,
        password,
        tenantName: tenantName || `${name.trim().split(' ')[0]} Home`,
        residenceName: residenceName || undefined,
      }),
    onSuccess: async (tokens) => {
      setError(null);
      await setSession(tokens);
    },
    onError: (err: AppApiError) => {
      setError(err.message || 'Não foi possível criar a conta');
    },
  });

  const canSubmit = name.trim() && email.trim() && password.length >= 8;

  return (
    <Screen variant="auth" padded={false} scroll>
      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        style={styles.root}
      >
        <View style={styles.hero}>
          <BrandMark
            compact
            tone="onDark"
            subtitle="Crie sua conta e a primeira residência"
          />
        </View>

        <AuthSheet>
          <Text style={styles.formTitle}>Criar conta</Text>
          <Text style={styles.formHint}>Configure o lar e comece a controlar acessos</Text>
          <TextField label="Seu nome" placeholder="Maria Silva" value={name} onChangeText={setName} />
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
            label="Senha (mín. 8)"
            secureTextEntry
            placeholder="••••••••"
            value={password}
            onChangeText={setPassword}
          />
          <TextField
            label="Nome do lar / condomínio"
            placeholder="Família Silva"
            value={tenantName}
            onChangeText={setTenantName}
          />
          <TextField
            label="Residência (opcional)"
            placeholder="Casa principal"
            value={residenceName}
            onChangeText={setResidenceName}
          />

          {error ? <Text style={styles.error}>{error}</Text> : null}

          <PrimaryButton
            label="Criar conta"
            variant="brand"
            loading={registerMutation.isPending}
            disabled={!canSubmit}
            onPress={() => registerMutation.mutate()}
            style={{ marginTop: 18 }}
          />

          <Pressable onPress={() => navigation.navigate('Login')} style={styles.linkWrap}>
            <Text style={styles.linkMuted}>Já tem conta?</Text>
            <Text style={styles.link}> Entrar</Text>
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
  linkWrap: {
    marginTop: 20,
    minHeight: 44,
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
  },
  linkMuted: { ...typography.body, color: colors.inkMuted },
  link: { ...typography.body, color: colors.brand, fontFamily: fonts.sansBold },
});


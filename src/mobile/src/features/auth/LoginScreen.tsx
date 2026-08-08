import { useState } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  useWindowDimensions,
  View,
} from 'react-native';
import { useMutation } from '@tanstack/react-query';
import { useNavigation } from '@react-navigation/native';
import type { NativeStackNavigationProp } from '@react-navigation/native-stack';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
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
  const insets = useSafeAreaInsets();
  const { height } = useWindowDimensions();
  const setSession = useSessionStore((s) => s.setSession);
  const bootstrapError = useSessionStore((s) => s.bootstrapError);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(bootstrapError);

  // Telas baixas: hero menor para sobrar espaço aos links
  const heroHeight = Math.round(Math.min(210, Math.max(112, height * 0.2)));
  const compactBrand = height < 720;

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
        keyboardVerticalOffset={Platform.OS === 'ios' ? 8 : 0}
      >
        <ScrollView
          style={styles.scrollView}
          contentContainerStyle={[
            styles.scrollContent,
            { paddingBottom: Math.max(insets.bottom, 12) + 8 },
          ]}
          keyboardShouldPersistTaps="handled"
          showsVerticalScrollIndicator={false}
          bounces
          alwaysBounceVertical={false}
        >
          <View style={[styles.hero, { height: heroHeight }]}>
            <BrandMark
              compact={compactBrand}
              tone="onDark"
              subtitle="Controle seguro da sua residência"
            />
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

            <PrimaryButton
              label="Entrar na residência"
              variant="brand"
              loading={loginMutation.isPending}
              disabled={!email || !password}
              onPress={() => loginMutation.mutate()}
              style={styles.primaryBtn}
            />

            <View style={styles.footerLinks}>
              <Pressable
                onPress={() => navigation.navigate('Register')}
                style={styles.linkBtn}
                hitSlop={12}
                accessibilityRole="button"
                accessibilityLabel="Criar conta"
              >
                <Text style={styles.linkPrimary}>Criar conta</Text>
              </Pressable>

              <Text style={styles.linkDot} accessibilityElementsHidden>
                ·
              </Text>

              <Pressable
                onPress={() => navigation.navigate('ForgotPassword')}
                style={styles.linkBtn}
                hitSlop={12}
                accessibilityRole="button"
                accessibilityLabel="Esqueci a senha"
              >
                <Text style={styles.linkSecondary}>Esqueci a senha</Text>
              </Pressable>
            </View>
          </AuthSheet>
        </ScrollView>
      </KeyboardAvoidingView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  root: { flex: 1 },
  scrollView: { flex: 1 },
  scrollContent: {
    flexGrow: 1,
    justifyContent: 'flex-end',
  },
  hero: {
    justifyContent: 'center',
    paddingHorizontal: 24,
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
  primaryBtn: { marginTop: 20 },
  footerLinks: {
    marginTop: 16,
    marginBottom: 4,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    flexWrap: 'wrap',
    gap: 4,
  },
  linkBtn: {
    minHeight: 48,
    justifyContent: 'center',
    paddingHorizontal: 10,
  },
  linkDot: {
    ...typography.caption,
    color: colors.inkFaint,
    marginHorizontal: 2,
  },
  linkPrimary: {
    ...typography.body,
    color: colors.brand,
    fontFamily: fonts.sansBold,
  },
  linkSecondary: {
    ...typography.body,
    color: colors.inkMuted,
    fontFamily: fonts.sansSemi,
  },
});


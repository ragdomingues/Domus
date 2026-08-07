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
import type { AppApiError } from '../../api/httpClient';
import type { RootStackParamList } from '../../navigation/types';
import { colors } from '../../theme/colors';
import { fonts, typography } from '../../theme/typography';
import { BrandMark } from '../../ui/BrandMark';
import { AuthSheet } from '../../ui/AuthSheet';
import { PrimaryButton } from '../../ui/PrimaryButton';
import { Screen } from '../../ui/Screen';
import { TextField } from '../../ui/TextField';
import { IconButton } from '../../ui/IconButton';

export function ForgotPasswordScreen() {
  const navigation = useNavigation<NativeStackNavigationProp<RootStackParamList>>();
  const [email, setEmail] = useState('');
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: () => authApi.forgotPassword(email),
    onSuccess: (data) => {
      setError(null);
      // Em Dev (ExposeResetToken), a API pode devolver o token para QA sem e-mail.
      navigation.navigate('ResetPassword', {
        email: email.trim(),
        resetToken: data.resetToken ?? undefined,
      });
    },
    onError: (err: AppApiError) => {
      setError(err.message || 'Não foi possível solicitar a redefinição');
    },
  });

  return (
    <Screen variant="auth" padded={false}>
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
            subtitle="Informe o e-mail da conta para redefinir a senha"
          />
        </View>

        <AuthSheet>
          <Text style={styles.formTitle}>Esqueci a senha</Text>
          <Text style={styles.formHint}>
            Enviaremos um código por e-mail. Depois você cola o código e define a nova senha.
          </Text>
          <TextField
            label="E-mail"
            autoCapitalize="none"
            autoCorrect={false}
            keyboardType="email-address"
            placeholder="voce@email.com"
            value={email}
            onChangeText={setEmail}
          />

          {error ? <Text style={styles.error}>{error}</Text> : null}

          <PrimaryButton
            label="Enviar código por e-mail"
            variant="brand"
            loading={mutation.isPending}
            disabled={!email.trim()}
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
  root: { flex: 1, justifyContent: 'flex-end' },
  hero: {
    flex: 1,
    justifyContent: 'center',
    paddingHorizontal: 24,
  },
  back: { position: 'absolute', top: 8, left: 16, zIndex: 2 },
  formTitle: {
    fontFamily: fonts.display,
    fontSize: 28,
    color: colors.ink,
    letterSpacing: -0.5,
    marginBottom: 6,
  },
  formHint: { ...typography.caption, color: colors.inkMuted, marginBottom: 6, lineHeight: 18 },
  error: { color: colors.danger, marginTop: 12, fontFamily: fonts.sansSemi },
  linkWrap: { marginTop: 20, alignItems: 'center' },
  link: { ...typography.caption, color: colors.brand, fontFamily: fonts.sansBold },
});

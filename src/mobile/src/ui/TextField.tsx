import { useEffect, useRef, useState } from 'react';
import {
  Platform,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View,
  type TextInputProps,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { colors } from '../theme/colors';
import { fonts, typography } from '../theme/typography';
import { radii } from '../theme/spacing';

type Props = TextInputProps & {
  label: string;
};

/**
 * No Android, TextInput controlado (`value`) costuma quebrar acentos no IME.
 * Usamos defaultValue no Android e só remonta se o valor mudar externamente.
 */
export function TextField({
  label,
  style,
  onFocus,
  onBlur,
  secureTextEntry,
  autoCorrect,
  autoCapitalize,
  value,
  defaultValue,
  onChangeText,
  ...rest
}: Props) {
  const [focused, setFocused] = useState(false);
  const [visible, setVisible] = useState(false);
  const isPassword = Boolean(secureTextEntry);
  const isAndroid = Platform.OS === 'android';

  const lastEmitted = useRef<string | undefined>(
    value !== undefined ? String(value) : undefined,
  );
  const [androidKey, setAndroidKey] = useState(0);
  const [androidDefault, setAndroidDefault] = useState(
    () => (value !== undefined ? String(value) : String(defaultValue ?? '')),
  );

  useEffect(() => {
    if (!isAndroid || value === undefined) return;
    const next = String(value);
    if (next === lastEmitted.current) return;
    lastEmitted.current = next;
    setAndroidDefault(next);
    setAndroidKey((k) => k + 1);
  }, [isAndroid, value]);

  return (
    <View style={styles.wrap}>
      <Text style={styles.label}>{label}</Text>
      <View style={[styles.inputWrap, focused && styles.inputWrapFocused]}>
        <TextInput
          key={isAndroid ? `android-${androidKey}` : 'ios'}
          {...rest}
          placeholderTextColor={colors.inkFaint}
          style={[styles.input, isPassword && styles.inputWithToggle, style]}
          {...(isPassword ? { secureTextEntry: !visible } : {})}
          autoCorrect={autoCorrect ?? (isPassword ? false : true)}
          autoCapitalize={autoCapitalize ?? (isPassword ? 'none' : 'sentences')}
          textAlignVertical="center"
          underlineColorAndroid="transparent"
          {...(isAndroid
            ? { defaultValue: androidDefault }
            : { value, defaultValue })}
          onChangeText={(text) => {
            lastEmitted.current = text;
            onChangeText?.(text);
          }}
          onFocus={(e) => {
            setFocused(true);
            onFocus?.(e);
          }}
          onBlur={(e) => {
            setFocused(false);
            onBlur?.(e);
          }}
        />
        {isPassword ? (
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={visible ? 'Ocultar senha' : 'Mostrar senha'}
            hitSlop={8}
            onPress={() => setVisible((v) => !v)}
            style={styles.toggle}
          >
            <Ionicons
              name={visible ? 'eye-off-outline' : 'eye-outline'}
              size={22}
              color={colors.inkMuted}
            />
          </Pressable>
        ) : null}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { marginTop: 14 },
  label: { ...typography.label, color: colors.inkMuted, marginBottom: 7 },
  inputWrap: {
    flexDirection: 'row',
    alignItems: 'center',
    borderWidth: 1.5,
    borderColor: colors.border,
    borderRadius: radii.md,
    backgroundColor: '#F3F7F4',
  },
  inputWrapFocused: {
    borderColor: colors.brandSoft,
    backgroundColor: colors.bgElevated,
  },
  input: {
    flex: 1,
    paddingHorizontal: 14,
    paddingVertical: Platform.OS === 'ios' ? 13 : 10,
    fontSize: 16,
    color: colors.ink,
    includeFontPadding: false,
    ...(Platform.OS === 'ios' ? { fontFamily: fonts.sans } : {}),
  },
  inputWithToggle: {
    paddingRight: 8,
  },
  toggle: {
    paddingHorizontal: 12,
    paddingVertical: 10,
    justifyContent: 'center',
    alignItems: 'center',
  },
});

import {
  ActivityIndicator,
  Pressable,
  StyleSheet,
  Text,
  type ViewStyle,
} from 'react-native';
import { colors } from '../theme/colors';
import { fonts } from '../theme/typography';
import { radii } from '../theme/spacing';

type Props = {
  label: string;
  onPress: () => void;
  disabled?: boolean;
  loading?: boolean;
  variant?: 'accent' | 'brand' | 'danger' | 'muted' | 'ghost';
  style?: ViewStyle;
};

const variants = {
  accent: { bg: colors.accent, pressed: colors.accentPressed, fg: '#fff', border: 'transparent' },
  brand: { bg: colors.brand, pressed: colors.brandSoft, fg: '#fff', border: 'transparent' },
  danger: { bg: colors.danger, pressed: '#912018', fg: '#fff', border: 'transparent' },
  muted: { bg: colors.bgElevated, pressed: colors.brandMist, fg: colors.ink, border: colors.border },
  ghost: { bg: 'transparent', pressed: colors.brandMist, fg: colors.brand, border: 'transparent' },
} as const;

export function PrimaryButton({
  label,
  onPress,
  disabled,
  loading,
  variant = 'accent',
  style,
}: Props) {
  const v = variants[variant];
  const isDisabled = disabled || loading;

  return (
    <Pressable
      accessibilityRole="button"
      disabled={isDisabled}
      onPress={onPress}
      style={({ pressed }) => [
        styles.btn,
        {
          backgroundColor: pressed ? v.pressed : v.bg,
          borderColor: v.border,
          opacity: isDisabled ? 0.45 : 1,
          transform: [{ scale: pressed && !isDisabled ? 0.98 : 1 }],
        },
        style,
      ]}
    >
      {loading ? (
        <ActivityIndicator color={v.fg} />
      ) : (
        <Text style={[styles.label, { color: v.fg }]}>{label}</Text>
      )}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  btn: {
    minHeight: 54,
    borderRadius: radii.md,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 18,
    borderWidth: 1,
  },
  label: {
    fontSize: 16,
    fontFamily: fonts.sansBold,
    letterSpacing: 0.2,
  },
});

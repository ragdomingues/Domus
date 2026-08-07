import { Pressable, StyleSheet } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { colors } from '../theme/colors';
import { radii } from '../theme/spacing';

type Props = {
  name: keyof typeof Ionicons.glyphMap;
  onPress: () => void;
  accessibilityLabel: string;
  tone?: 'muted' | 'brand' | 'onDark';
};

export function IconButton({ name, onPress, accessibilityLabel, tone = 'muted' }: Props) {
  const palette =
    tone === 'brand'
      ? { bg: colors.brand, fg: '#fff', border: colors.brand }
      : tone === 'onDark'
        ? { bg: 'rgba(244,248,245,0.14)', fg: '#F4F8F5', border: 'rgba(244,248,245,0.28)' }
        : { bg: colors.bgElevated, fg: colors.ink, border: colors.border };

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel}
      onPress={onPress}
      style={({ pressed }) => [
        styles.btn,
        {
          backgroundColor: palette.bg,
          borderColor: palette.border,
          opacity: pressed ? 0.85 : 1,
        },
      ]}
    >
      <Ionicons name={name} size={20} color={palette.fg} />
    </Pressable>
  );
}

const styles = StyleSheet.create({
  btn: {
    width: 44,
    height: 44,
    borderRadius: radii.pill,
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1,
  },
});

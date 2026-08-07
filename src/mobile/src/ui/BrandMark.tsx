import { useEffect, useRef } from 'react';
import { Animated, Easing, StyleSheet, Text, View } from 'react-native';
import { colors } from '../theme/colors';
import { fonts, typography } from '../theme/typography';
import { DomusLogo } from './DomusLogo';

type Props = {
  subtitle?: string;
  compact?: boolean;
  tone?: 'light' | 'onDark';
};

export function BrandMark({ subtitle, compact, tone = 'light' }: Props) {
  const opacity = useRef(new Animated.Value(0)).current;
  const translateY = useRef(new Animated.Value(18)).current;
  const onDark = tone === 'onDark';
  const size = compact ? 76 : 104;

  useEffect(() => {
    Animated.parallel([
      Animated.timing(opacity, {
        toValue: 1,
        duration: 720,
        easing: Easing.out(Easing.cubic),
        useNativeDriver: true,
      }),
      Animated.timing(translateY, {
        toValue: 0,
        duration: 720,
        easing: Easing.out(Easing.cubic),
        useNativeDriver: true,
      }),
    ]).start();
  }, [opacity, translateY]);

  return (
    <Animated.View style={[styles.wrap, { opacity, transform: [{ translateY }] }]}>
      <DomusLogo size={size} tone={tone} animated />
      <Text style={[styles.brand, compact && styles.brandCompact, onDark && styles.brandOnDark]}>
        Domus
      </Text>
      <View style={[styles.rule, onDark && styles.ruleOnDark]} />
      {subtitle ? (
        <Text style={[styles.subtitle, onDark && styles.subtitleOnDark]}>{subtitle}</Text>
      ) : null}
    </Animated.View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    alignItems: 'center',
    marginBottom: 4,
  },
  brand: {
    fontFamily: fonts.display,
    fontSize: 52,
    letterSpacing: -1.8,
    color: colors.brand,
    lineHeight: 56,
    marginTop: 18,
    textAlign: 'center',
  },
  brandCompact: { fontSize: 38, lineHeight: 42, marginTop: 14 },
  brandOnDark: { color: colors.onDark },
  rule: {
    width: 36,
    height: 3,
    borderRadius: 2,
    backgroundColor: colors.accent,
    marginTop: 12,
    marginBottom: 10,
  },
  ruleOnDark: { backgroundColor: colors.accent },
  subtitle: {
    ...typography.subtitle,
    color: colors.inkMuted,
    maxWidth: 280,
    textAlign: 'center',
    lineHeight: 22,
  },
  subtitleOnDark: { color: colors.onDarkMuted },
});

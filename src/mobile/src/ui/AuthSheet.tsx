import { type ReactNode, useEffect, useRef } from 'react';
import { Animated, Easing, StyleSheet, View } from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';
import { colors } from '../theme/colors';
import { radii } from '../theme/spacing';

type Props = {
  children: ReactNode;
  delay?: number;
};

export function AuthSheet({ children, delay = 160 }: Props) {
  const translateY = useRef(new Animated.Value(56)).current;
  const opacity = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    Animated.parallel([
      Animated.timing(translateY, {
        toValue: 0,
        duration: 640,
        delay,
        easing: Easing.out(Easing.cubic),
        useNativeDriver: true,
      }),
      Animated.timing(opacity, {
        toValue: 1,
        duration: 480,
        delay,
        useNativeDriver: true,
      }),
    ]).start();
  }, [delay, opacity, translateY]);

  return (
    <Animated.View style={[styles.sheet, { opacity, transform: [{ translateY }] }]}>
      <View style={styles.accentRow}>
        <LinearGradient
          colors={[colors.accent, colors.brandSoft]}
          start={{ x: 0, y: 0 }}
          end={{ x: 1, y: 0 }}
          style={styles.topLine}
        />
      </View>
      {children}
    </Animated.View>
  );
}

const styles = StyleSheet.create({
  sheet: {
    backgroundColor: colors.bgElevated,
    borderTopLeftRadius: 28,
    borderTopRightRadius: 28,
    paddingHorizontal: 24,
    paddingTop: 22,
    paddingBottom: 32,
    minHeight: '54%',
    shadowColor: colors.shadow,
    shadowOpacity: 1,
    shadowRadius: 32,
    shadowOffset: { width: 0, height: -12 },
    elevation: 16,
  },
  accentRow: {
    marginBottom: 18,
  },
  topLine: {
    height: 3,
    borderRadius: radii.pill,
    width: 48,
  },
});

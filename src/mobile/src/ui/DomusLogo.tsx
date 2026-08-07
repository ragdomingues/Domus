import { useEffect, useRef } from 'react';
import { Animated, Easing, Image, StyleSheet, View } from 'react-native';
import Svg, { Circle } from 'react-native-svg';
import { colors } from '../theme/colors';

type Props = {
  size?: number;
  animated?: boolean;
  tone?: 'light' | 'onDark';
};

/** Marca circular com anéis — mais nítida que o crop quadrado genérico. */
export function DomusLogo({ size = 88, animated = true, tone = 'light' }: Props) {
  const spin = useRef(new Animated.Value(0)).current;
  const pulse = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    if (!animated) return;
    const spinLoop = Animated.loop(
      Animated.timing(spin, {
        toValue: 1,
        duration: 16000,
        easing: Easing.linear,
        useNativeDriver: true,
      }),
    );
    const pulseLoop = Animated.loop(
      Animated.sequence([
        Animated.timing(pulse, {
          toValue: 1,
          duration: 1600,
          easing: Easing.inOut(Easing.sin),
          useNativeDriver: true,
        }),
        Animated.timing(pulse, {
          toValue: 0,
          duration: 1600,
          easing: Easing.inOut(Easing.sin),
          useNativeDriver: true,
        }),
      ]),
    );
    spinLoop.start();
    pulseLoop.start();
    return () => {
      spinLoop.stop();
      pulseLoop.stop();
    };
  }, [animated, pulse, spin]);

  const ring = size + 22;
  const rotate = spin.interpolate({ inputRange: [0, 1], outputRange: ['0deg', '360deg'] });
  const ringOpacity = pulse.interpolate({ inputRange: [0, 1], outputRange: [0.35, 0.85] });
  const stroke = tone === 'onDark' ? 'rgba(244,248,245,0.55)' : colors.brandSoft;
  const accent = colors.accent;

  return (
    <View style={{ width: ring, height: ring, alignItems: 'center', justifyContent: 'center' }}>
      <Animated.View
        style={[
          StyleSheet.absoluteFill,
          { transform: [{ rotate }], opacity: animated ? ringOpacity : 0.5 },
        ]}
      >
        <Svg width={ring} height={ring}>
          <Circle
            cx={ring / 2}
            cy={ring / 2}
            r={ring / 2 - 2}
            stroke={stroke}
            strokeWidth={1.4}
            fill="none"
            strokeDasharray="4 8"
          />
          <Circle cx={ring / 2} cy={3} r={3} fill={accent} />
        </Svg>
      </Animated.View>
      <View
        style={[
          styles.disc,
          {
            width: size,
            height: size,
            borderRadius: size / 2,
            borderColor: tone === 'onDark' ? 'rgba(244,248,245,0.35)' : colors.border,
          },
        ]}
      >
        <Image
          source={require('../../assets/brand/domus-mark.png')}
          style={{ width: size, height: size, borderRadius: size / 2 }}
          resizeMode="cover"
          accessibilityLabel="Domus"
        />
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  disc: {
    overflow: 'hidden',
    backgroundColor: colors.bgElevated,
    borderWidth: 1.5,
  },
});

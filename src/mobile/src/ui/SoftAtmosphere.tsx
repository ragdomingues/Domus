import { useEffect, useRef } from 'react';
import { Animated, Easing, StyleSheet, View } from 'react-native';
import Svg, { Circle } from 'react-native-svg';
import { colors } from '../theme/colors';

/** Atmosfera leve para telas autenticadas — presença sem competir com o conteúdo. */
export function SoftAtmosphere() {
  const spin = useRef(new Animated.Value(0)).current;
  const pulse = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    const spinLoop = Animated.loop(
      Animated.timing(spin, {
        toValue: 1,
        duration: 42000,
        easing: Easing.linear,
        useNativeDriver: true,
      }),
    );
    const pulseLoop = Animated.loop(
      Animated.sequence([
        Animated.timing(pulse, {
          toValue: 1,
          duration: 2800,
          easing: Easing.inOut(Easing.sin),
          useNativeDriver: true,
        }),
        Animated.timing(pulse, {
          toValue: 0,
          duration: 2800,
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
  }, [pulse, spin]);

  const rotate = spin.interpolate({ inputRange: [0, 1], outputRange: ['0deg', '360deg'] });
  const opacity = pulse.interpolate({ inputRange: [0, 1], outputRange: [0.18, 0.38] });

  return (
    <View pointerEvents="none" style={styles.wrap}>
      <Animated.View style={{ transform: [{ rotate }], opacity }}>
        <Svg width={220} height={220}>
          <Circle
            cx={110}
            cy={110}
            r={96}
            stroke={colors.brand}
            strokeWidth={1}
            fill="none"
            strokeOpacity={0.35}
          />
          <Circle
            cx={110}
            cy={110}
            r={70}
            stroke={colors.accent}
            strokeWidth={1}
            fill="none"
            strokeOpacity={0.28}
            strokeDasharray="5 9"
          />
          <Circle cx={110} cy={14} r={3.5} fill={colors.accent} opacity={0.7} />
        </Svg>
      </Animated.View>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    position: 'absolute',
    top: -40,
    right: -70,
    width: 220,
    height: 220,
  },
});

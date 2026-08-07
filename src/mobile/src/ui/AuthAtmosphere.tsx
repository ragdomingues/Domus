import { useEffect, useRef } from 'react';
import { Animated, Dimensions, Easing, StyleSheet, View } from 'react-native';
import Svg, { Circle, Defs, Line, LinearGradient as SvgGradient, Path, Stop } from 'react-native-svg';
import { colors } from '../theme/colors';

const { width: W, height: H } = Dimensions.get('window');

/**
 * Camada gráfica contínua para auth: órbitas, nós IoT e varredura.
 * Motion lento e deliberado — presença, não ruído.
 */
export function AuthAtmosphere() {
  const spin = useRef(new Animated.Value(0)).current;
  const pulse = useRef(new Animated.Value(0)).current;
  const drift = useRef(new Animated.Value(0)).current;
  const scan = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    const spinLoop = Animated.loop(
      Animated.timing(spin, {
        toValue: 1,
        duration: 28000,
        easing: Easing.linear,
        useNativeDriver: true,
      }),
    );
    const pulseLoop = Animated.loop(
      Animated.sequence([
        Animated.timing(pulse, {
          toValue: 1,
          duration: 2200,
          easing: Easing.inOut(Easing.sin),
          useNativeDriver: true,
        }),
        Animated.timing(pulse, {
          toValue: 0,
          duration: 2200,
          easing: Easing.inOut(Easing.sin),
          useNativeDriver: true,
        }),
      ]),
    );
    const driftLoop = Animated.loop(
      Animated.sequence([
        Animated.timing(drift, {
          toValue: 1,
          duration: 9000,
          easing: Easing.inOut(Easing.quad),
          useNativeDriver: true,
        }),
        Animated.timing(drift, {
          toValue: 0,
          duration: 9000,
          easing: Easing.inOut(Easing.quad),
          useNativeDriver: true,
        }),
      ]),
    );
    const scanLoop = Animated.loop(
      Animated.timing(scan, {
        toValue: 1,
        duration: 6500,
        easing: Easing.inOut(Easing.cubic),
        useNativeDriver: true,
      }),
    );

    spinLoop.start();
    pulseLoop.start();
    driftLoop.start();
    scanLoop.start();

    return () => {
      spinLoop.stop();
      pulseLoop.stop();
      driftLoop.stop();
      scanLoop.stop();
    };
  }, [spin, pulse, drift, scan]);

  const rotate = spin.interpolate({ inputRange: [0, 1], outputRange: ['0deg', '360deg'] });
  const ringScale = pulse.interpolate({ inputRange: [0, 1], outputRange: [1, 1.06] });
  const ringOpacity = pulse.interpolate({ inputRange: [0, 1], outputRange: [0.35, 0.7] });
  const floatY = drift.interpolate({ inputRange: [0, 1], outputRange: [0, -18] });
  const scanY = scan.interpolate({ inputRange: [0, 1], outputRange: [H * 0.12, H * 0.55] });
  const arcOpacity = pulse.interpolate({ inputRange: [0, 1], outputRange: [0.35, 0.8] });

  return (
    <View pointerEvents="none" style={StyleSheet.absoluteFill}>
      <Svg width={W} height={H} style={StyleSheet.absoluteFill}>
        <Defs>
          <SvgGradient id="mesh" x1="0" y1="0" x2="1" y2="1">
            <Stop offset="0" stopColor={colors.brand} stopOpacity="0.12" />
            <Stop offset="0.55" stopColor={colors.brandMist} stopOpacity="0.28" />
            <Stop offset="1" stopColor={colors.accent} stopOpacity="0.1" />
          </SvgGradient>
        </Defs>
        <Path d={`M0 0 H${W} V${H} H0 Z`} fill="url(#mesh)" />
        {Array.from({ length: 7 }).map((_, i) => (
          <Line
            key={`h-${i}`}
            x1={0}
            y1={H * 0.08 + i * 42}
            x2={W}
            y2={H * 0.08 + i * 42}
            stroke="#F4F8F5"
            strokeOpacity={0.05}
            strokeWidth={1}
          />
        ))}
        {Array.from({ length: 5 }).map((_, i) => (
          <Line
            key={`v-${i}`}
            x1={W * 0.12 + i * (W * 0.2)}
            y1={0}
            x2={W * 0.12 + i * (W * 0.2)}
            y2={H * 0.62}
            stroke="#F4F8F5"
            strokeOpacity={0.04}
            strokeWidth={1}
          />
        ))}
      </Svg>

      <Animated.View
        style={[
          styles.scan,
          {
            transform: [{ translateY: scanY }],
            opacity: scan.interpolate({ inputRange: [0, 0.5, 1], outputRange: [0, 0.5, 0] }),
          },
        ]}
      />

      <Animated.View
        style={[
          styles.orbitWrap,
          { transform: [{ rotate }, { scale: ringScale }], opacity: ringOpacity },
        ]}
      >
        <Svg width={280} height={280}>
          <Circle
            cx={140}
            cy={140}
            r={118}
            stroke="#E7F0EB"
            strokeOpacity={0.35}
            strokeWidth={1.5}
            fill="none"
          />
          <Circle
            cx={140}
            cy={140}
            r={88}
            stroke={colors.accent}
            strokeOpacity={0.45}
            strokeWidth={1.2}
            fill="none"
            strokeDasharray="6 10"
          />
          <Circle cx={140} cy={22} r={5} fill={colors.accent} />
          <Circle cx={248} cy={170} r={4} fill="#E7F0EB" />
          <Circle cx={40} cy={160} r={3.5} fill="#E7F0EB" opacity={0.75} />
        </Svg>
      </Animated.View>

      <Animated.View style={[styles.emblem, { transform: [{ translateY: floatY }] }]}>
        <Svg width={150} height={120} viewBox="0 0 150 120">
          <Circle cx={75} cy={64} r={48} fill="#F4F8F5" opacity={0.08} />
          <Path
            d="M42 82 L42 54 L75 28 L108 54 L108 82 Z"
            stroke="#F4F8F5"
            strokeWidth={3}
            fill="none"
            strokeLinejoin="round"
          />
          <Path d="M62 82 L62 60 L88 60 L88 82" stroke="#F4F8F5" strokeWidth={3} fill="none" />
          <Circle cx={75} cy={52} r={3.5} fill={colors.accent} />
        </Svg>
      </Animated.View>

      <Animated.View style={[styles.bottomArc, { opacity: arcOpacity }]}>
        <Svg width={W} height={160}>
          <Path
            d={`M ${-40} 130 Q ${W / 2} 18 ${W + 40} 130`}
            stroke="#E7F0EB"
            strokeWidth={1.5}
            fill="none"
            strokeOpacity={0.35}
          />
          <Path
            d={`M ${16} 148 Q ${W / 2} 48 ${W - 16} 148`}
            stroke={colors.accent}
            strokeWidth={1.2}
            fill="none"
            strokeOpacity={0.35}
          />
        </Svg>
      </Animated.View>
    </View>
  );
}

const styles = StyleSheet.create({
  orbitWrap: {
    position: 'absolute',
    top: H * 0.05,
    left: (W - 280) / 2,
  },
  emblem: {
    position: 'absolute',
    top: H * 0.13,
    left: (W - 150) / 2,
  },
  scan: {
    position: 'absolute',
    left: 24,
    right: 24,
    height: 1.5,
    borderRadius: 1,
    backgroundColor: colors.accent,
  },
  bottomArc: {
    position: 'absolute',
    bottom: H * 0.3,
    left: 0,
  },
});

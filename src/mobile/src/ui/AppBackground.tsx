import { type ReactNode } from 'react';
import { StyleSheet, View } from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';
import { colors } from '../theme/colors';
import { AuthAtmosphere } from './AuthAtmosphere';
import { SoftAtmosphere } from './SoftAtmosphere';

type Props = {
  children: ReactNode;
  variant?: 'auth' | 'app';
};

export function AppBackground({ children, variant = 'app' }: Props) {
  if (variant === 'auth') {
    return (
      <View style={styles.root}>
        <LinearGradient
          colors={['#071F1A', '#0B2E26', '#1A463A', '#C9D7CF']}
          locations={[0, 0.32, 0.62, 1]}
          start={{ x: 0.2, y: 0 }}
          end={{ x: 0.8, y: 1 }}
          style={StyleSheet.absoluteFill}
        />
        <AuthAtmosphere />
        {children}
      </View>
    );
  }

  return (
    <View style={styles.root}>
      <LinearGradient
        colors={['#DCE8E1', colors.bg, colors.bgElevated]}
        locations={[0, 0.35, 1]}
        start={{ x: 0.2, y: 0 }}
        end={{ x: 0.8, y: 1 }}
        style={StyleSheet.absoluteFill}
      />
      <SoftAtmosphere />
      {children}
    </View>
  );
}

const styles = StyleSheet.create({
  root: { flex: 1 },
});

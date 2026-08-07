import { StyleSheet, Text, View } from 'react-native';
import { colors } from '../theme/colors';
import { fonts, typography } from '../theme/typography';
import { DomusLogo } from './DomusLogo';
import { IconButton } from './IconButton';

type Props = {
  title?: string;
  subtitle?: string;
  onSettings?: () => void;
  onLogout?: () => void;
};

export function AppHeader({ title = 'Domus', subtitle, onSettings, onLogout }: Props) {
  return (
    <View style={styles.wrap}>
      <DomusLogo size={48} animated={false} />
      <View style={styles.textCol}>
        <Text style={styles.title}>{title}</Text>
        {subtitle ? <Text style={styles.subtitle}>{subtitle}</Text> : null}
      </View>
      {onSettings ? (
        <IconButton name="settings-outline" accessibilityLabel="Configurações" onPress={onSettings} />
      ) : null}
      {onLogout ? (
        <IconButton name="log-out-outline" accessibilityLabel="Sair" onPress={onLogout} />
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    marginBottom: 22,
  },
  textCol: { flex: 1 },
  title: {
    fontFamily: fonts.display,
    fontSize: 30,
    letterSpacing: -0.8,
    color: colors.brand,
  },
  subtitle: {
    ...typography.caption,
    color: colors.inkMuted,
    marginTop: 2,
  },
});

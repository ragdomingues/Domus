import { StyleSheet, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { colors } from '../theme/colors';
import { fonts } from '../theme/typography';

type Props = {
  tone: 'connecting' | 'offline' | 'info' | 'success' | 'danger';
  message: string;
};

const toneStyle = {
  connecting: { bg: colors.connecting, fg: '#fff', icon: 'sync-outline' as const },
  offline: { bg: colors.offlineBanner, fg: '#fff', icon: 'cloud-offline-outline' as const },
  info: { bg: colors.brandSoft, fg: '#fff', icon: 'information-circle-outline' as const },
  success: { bg: colors.success, fg: '#fff', icon: 'checkmark-circle-outline' as const },
  danger: { bg: colors.danger, fg: '#fff', icon: 'alert-circle-outline' as const },
} as const;

export function StatusBanner({ tone, message }: Props) {
  const t = toneStyle[tone];
  return (
    <View style={[styles.wrap, { backgroundColor: t.bg }]}>
      <Ionicons name={t.icon} size={16} color={t.fg} style={{ marginRight: 8 }} />
      <Text style={[styles.text, { color: t.fg }]}>{message}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    paddingHorizontal: 16,
    paddingVertical: 11,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
  },
  text: {
    fontSize: 13,
    fontFamily: fonts.sansSemi,
    textAlign: 'center',
  },
});

import { StyleSheet, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { colors } from '../theme/colors';
import { fonts, typography } from '../theme/typography';
import { radii } from '../theme/spacing';

type Props = {
  icon?: keyof typeof Ionicons.glyphMap;
  title: string;
  description: string;
};

export function EmptyState({ icon = 'home-outline', title, description }: Props) {
  return (
    <View style={styles.wrap}>
      <View style={styles.iconOuter}>
        <View style={styles.iconRing}>
          <Ionicons name={icon} size={26} color={colors.brand} />
        </View>
      </View>
      <Text style={styles.title}>{title}</Text>
      <Text style={styles.description}>{description}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    alignItems: 'center',
    paddingVertical: 40,
    paddingHorizontal: 20,
  },
  iconOuter: {
    width: 84,
    height: 84,
    borderRadius: 42,
    borderWidth: 1,
    borderColor: colors.border,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 18,
    backgroundColor: 'rgba(251,252,250,0.65)',
  },
  iconRing: {
    width: 56,
    height: 56,
    borderRadius: radii.xl,
    backgroundColor: colors.brandMist,
    alignItems: 'center',
    justifyContent: 'center',
  },
  title: {
    fontFamily: fonts.display,
    fontSize: 22,
    letterSpacing: -0.4,
    color: colors.brand,
    textAlign: 'center',
  },
  description: {
    ...typography.caption,
    color: colors.inkMuted,
    textAlign: 'center',
    marginTop: 8,
    maxWidth: 280,
    lineHeight: 20,
  },
});

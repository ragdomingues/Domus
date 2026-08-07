import { TextStyle } from 'react-native';

/**
 * Domus type system — geometric modern.
 * Outfit for brand/titles; Plus Jakarta Sans for UI.
 */
export const fonts = {
  display: 'Outfit_700Bold',
  displayRegular: 'Outfit_500Medium',
  sans: 'PlusJakartaSans_400Regular',
  sansMedium: 'PlusJakartaSans_500Medium',
  sansSemi: 'PlusJakartaSans_600SemiBold',
  sansBold: 'PlusJakartaSans_700Bold',
} as const;

export const typography = {
  brand: {
    fontFamily: fonts.display,
    fontSize: 48,
    letterSpacing: -1.4,
    lineHeight: 52,
  } satisfies TextStyle,
  title: {
    fontFamily: fonts.sansBold,
    fontSize: 22,
    letterSpacing: -0.4,
  } satisfies TextStyle,
  subtitle: {
    fontFamily: fonts.sansMedium,
    fontSize: 16,
    lineHeight: 24,
  } satisfies TextStyle,
  body: {
    fontFamily: fonts.sans,
    fontSize: 16,
    lineHeight: 24,
  } satisfies TextStyle,
  label: {
    fontFamily: fonts.sansSemi,
    fontSize: 13,
    letterSpacing: 0.3,
  } satisfies TextStyle,
  caption: {
    fontFamily: fonts.sansMedium,
    fontSize: 13,
    lineHeight: 18,
  } satisfies TextStyle,
};

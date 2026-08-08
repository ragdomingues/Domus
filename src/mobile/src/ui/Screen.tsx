import { type ReactElement, type ReactNode } from 'react';
import {
  ScrollView,
  StyleSheet,
  View,
  type RefreshControlProps,
  type ViewStyle,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { AppBackground } from './AppBackground';

type Props = {
  children: ReactNode;
  scroll?: boolean;
  style?: ViewStyle;
  refreshControl?: ReactElement<RefreshControlProps>;
  variant?: 'auth' | 'app';
  padded?: boolean;
};

export function Screen({
  children,
  scroll,
  style,
  refreshControl,
  variant = 'app',
  padded = true,
}: Props) {
  const contentStyle = [padded ? styles.content : styles.contentFlush, style];

  return (
    <AppBackground variant={variant}>
      <SafeAreaView
        style={styles.safe}
        edges={variant === 'auth' ? ['top', 'left', 'right'] : ['top', 'left', 'right', 'bottom']}
      >
        {scroll ? (
          <ScrollView
            contentContainerStyle={contentStyle}
            refreshControl={refreshControl}
            keyboardShouldPersistTaps="handled"
            showsVerticalScrollIndicator={false}
          >
            {children}
          </ScrollView>
        ) : (
          <View style={contentStyle}>{children}</View>
        )}
      </SafeAreaView>
    </AppBackground>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1 },
  content: {
    flexGrow: 1,
    paddingHorizontal: 22,
    paddingTop: 12,
    paddingBottom: 28,
  },
  contentFlush: {
    flexGrow: 1,
  },
});

import { useEffect, useState } from 'react';
import { ActivityIndicator, LogBox, View } from 'react-native';
import { StatusBar } from 'expo-status-bar';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import {
  useFonts,
  Outfit_500Medium,
  Outfit_700Bold,
} from '@expo-google-fonts/outfit';
import {
  PlusJakartaSans_400Regular,
  PlusJakartaSans_500Medium,
  PlusJakartaSans_600SemiBold,
  PlusJakartaSans_700Bold,
} from '@expo-google-fonts/plus-jakarta-sans';
import { Ionicons } from '@expo/vector-icons';
import { AppProviders } from './src/providers/AppProviders';
import { RootNavigator } from './src/navigation/RootNavigator';
import { colors } from './src/theme/colors';

// Avisos de rede/dev não devem cobrir a UI no celular
LogBox.ignoreLogs([
  '[Domus:',
  'SignalR',
  'expo-notifications',
  'Cannot connect to Expo CLI',
  'stopped during negotiation',
  'Network request failed',
]);

export default function App() {
  const [fontsLoaded] = useFonts({
    Outfit_500Medium,
    Outfit_700Bold,
    PlusJakartaSans_400Regular,
    PlusJakartaSans_500Medium,
    PlusJakartaSans_600SemiBold,
    PlusJakartaSans_700Bold,
    ...Ionicons.font,
  });
  const [bootMinElapsed, setBootMinElapsed] = useState(false);

  useEffect(() => {
    const t = setTimeout(() => setBootMinElapsed(true), 200);
    return () => clearTimeout(t);
  }, []);

  if (!fontsLoaded || !bootMinElapsed) {
    return (
      <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.bg }}>
        <ActivityIndicator size="large" color={colors.brand} />
      </View>
    );
  }

  return (
    <SafeAreaProvider>
      <AppProviders>
        <RootNavigator />
        <StatusBar style="dark" />
      </AppProviders>
    </SafeAreaProvider>
  );
}

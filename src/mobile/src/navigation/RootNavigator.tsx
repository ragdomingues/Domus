import { ActivityIndicator, Platform, StyleSheet, View } from 'react-native';
import { NavigationContainer, DefaultTheme } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { Ionicons } from '@expo/vector-icons';
import { useSessionStore } from '../auth/sessionStore';
import { LoginScreen } from '../features/auth/LoginScreen';
import { RegisterScreen } from '../features/auth/RegisterScreen';
import { ForgotPasswordScreen } from '../features/auth/ForgotPasswordScreen';
import { ResetPasswordScreen } from '../features/auth/ResetPasswordScreen';
import { DashboardScreen } from '../features/dashboard/DashboardScreen';
import { DeviceFormScreen } from '../features/devices/DeviceFormScreen';
import { GateControlScreen } from '../features/gate/GateControlScreen';
import { HistoryScreen } from '../features/history/HistoryScreen';
import { ResidenceFormScreen } from '../features/residences/ResidenceFormScreen';
import { NotificationsScreen } from '../features/notifications/NotificationsScreen';
import { SettingsScreen } from '../features/settings/SettingsScreen';
import { UsersScreen } from '../features/users/UsersScreen';
import type { MainTabParamList, RootStackParamList } from './types';
import { navigationRef } from './navigationRef';
import { linking } from './linking';
import { colors } from '../theme/colors';
import { fonts } from '../theme/typography';
import { AppBackground } from '../ui/AppBackground';

const Stack = createNativeStackNavigator<RootStackParamList>();
const Tabs = createBottomTabNavigator<MainTabParamList>();

const navTheme = {
  ...DefaultTheme,
  colors: {
    ...DefaultTheme.colors,
    background: 'transparent',
    card: colors.bgElevated,
    text: colors.ink,
    border: colors.border,
    primary: colors.brand,
  },
};

function MainTabs() {
  return (
    <Tabs.Navigator
      screenOptions={({ route }) => ({
        headerShown: false,
        tabBarActiveTintColor: colors.brand,
        tabBarInactiveTintColor: colors.inkFaint,
        tabBarLabelStyle: {
          fontFamily: fonts.sansSemi,
          fontSize: 12,
          marginBottom: Platform.OS === 'ios' ? 0 : 4,
        },
        tabBarStyle: {
          backgroundColor: colors.bgElevated,
          borderTopColor: colors.border,
          height: Platform.OS === 'ios' ? 88 : 68,
          paddingTop: 8,
          shadowColor: colors.shadow,
          shadowOpacity: 1,
          shadowRadius: 16,
          shadowOffset: { width: 0, height: -4 },
          elevation: 10,
        },
        tabBarIcon: ({ color, size, focused }) => {
          const icon =
            route.name === 'Home'
              ? focused
                ? 'home'
                : 'home-outline'
              : focused
                ? 'people'
                : 'people-outline';
          return <Ionicons name={icon} size={size} color={color} />;
        },
      })}
    >
      <Tabs.Screen name="Home" component={DashboardScreen} options={{ title: 'Residência' }} />
      <Tabs.Screen name="Users" component={UsersScreen} options={{ title: 'Usuários' }} />
    </Tabs.Navigator>
  );
}

export function RootNavigator() {
  const hydrated = useSessionStore((s) => s.hydrated);
  const isAuthenticated = useSessionStore((s) => s.isAuthenticated);

  if (!hydrated) {
    return (
      <AppBackground>
        <View style={styles.boot}>
          <ActivityIndicator size="large" color={colors.brand} />
        </View>
      </AppBackground>
    );
  }

  return (
    <NavigationContainer ref={navigationRef} theme={navTheme} linking={linking}>
      <Stack.Navigator screenOptions={{ headerShown: false, contentStyle: { backgroundColor: 'transparent' } }}>
        {!isAuthenticated ? (
          <>
            <Stack.Screen name="Login" component={LoginScreen} />
            <Stack.Screen name="Register" component={RegisterScreen} />
            <Stack.Screen name="ForgotPassword" component={ForgotPasswordScreen} />
            <Stack.Screen name="ResetPassword" component={ResetPasswordScreen} />
          </>
        ) : (
          <>
            <Stack.Screen name="MainTabs" component={MainTabs} />
            <Stack.Screen name="GateControl" component={GateControlScreen} />
            <Stack.Screen name="History" component={HistoryScreen} />
            <Stack.Screen name="Settings" component={SettingsScreen} />
            <Stack.Screen name="Notifications" component={NotificationsScreen} />
            <Stack.Screen name="ResidenceForm" component={ResidenceFormScreen} />
            <Stack.Screen name="DeviceForm" component={DeviceFormScreen} />
          </>
        )}
      </Stack.Navigator>
    </NavigationContainer>
  );
}

const styles = StyleSheet.create({
  boot: { flex: 1, alignItems: 'center', justifyContent: 'center' },
});

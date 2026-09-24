import { Stack } from 'expo-router';
import { useTheme } from '@/theme';

// Écran affiché en premier dans ce groupe (sinon l'ordre n'est pas garanti).
export const unstable_settings = {
  initialRouteName: 'login',
};

export default function AuthLayout() {
  const theme = useTheme();
  return (
    <Stack
      screenOptions={{
        headerStyle: { backgroundColor: theme.colors.bg },
        headerShadowVisible: false,
        headerTintColor: theme.colors.ink,
        headerTitleStyle: { fontFamily: theme.fonts.heading, fontSize: theme.type.card.fontSize, color: theme.colors.ink },
        headerBackButtonDisplayMode: 'minimal',
        contentStyle: { backgroundColor: theme.colors.bg },
      }}
    >
      <Stack.Screen name="login" options={{ headerShown: false }} />
      <Stack.Screen name="register" options={{ title: 'Créer un compte' }} />
    </Stack>
  );
}

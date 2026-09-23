import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { ActivityIndicator, StyleSheet, Text, View } from 'react-native';
import { AuthProvider, useAuth } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { colors, spacing } from '@/theme';

export default function RootLayout() {
  return (
    <AuthProvider>
      <StatusBar style="dark" />
      <RootNavigator />
    </AuthProvider>
  );
}

/**
 * Navigation protégée : les groupes (app) et (auth) ne sont accessibles que
 * si leur « guard » est vrai. Quand l'état de session change (connexion,
 * déconnexion, session expirée), Expo Router redirige automatiquement.
 */
function RootNavigator() {
  const { state, retry } = useAuth();

  if (state.status === 'loading') {
    return (
      <View style={styles.centered}>
        <ActivityIndicator size="large" color={colors.primary} />
      </View>
    );
  }

  if (state.status === 'unreachable') {
    return (
      <View style={styles.centered}>
        <Text style={styles.message}>{state.message}</Text>
        <Button title="Réessayer" onPress={() => void retry()} />
      </View>
    );
  }

  const signedIn = state.status === 'signedIn';

  return (
    <Stack screenOptions={{ headerShown: false }}>
      <Stack.Protected guard={signedIn}>
        <Stack.Screen name="(app)" />
      </Stack.Protected>
      <Stack.Protected guard={!signedIn}>
        <Stack.Screen name="(auth)" />
      </Stack.Protected>
    </Stack>
  );
}

const styles = StyleSheet.create({
  centered: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: spacing.md,
    padding: spacing.lg,
    backgroundColor: colors.background,
  },
  message: { fontSize: 16, color: colors.text, textAlign: 'center' },
});

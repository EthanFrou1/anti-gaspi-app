import { Figtree_400Regular, Figtree_500Medium, Figtree_700Bold, Figtree_800ExtraBold } from '@expo-google-fonts/figtree';
import { Fredoka_600SemiBold, Fredoka_700Bold } from '@expo-google-fonts/fredoka';
import { useFonts } from 'expo-font';
import { Stack } from 'expo-router';
import * as SplashScreen from 'expo-splash-screen';
import { StatusBar } from 'expo-status-bar';
import { useEffect } from 'react';
import { ActivityIndicator, StyleSheet, Text, View } from 'react-native';
import { AuthProvider, useAuth } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { useExpiryReminderSync } from '@/features/notifications/hooks';
import { colors, spacing, ThemeProvider, useTheme } from '@/theme';

// Le splash reste affiché tant que les polices de la charte ne sont pas chargées :
// sinon, les textes s'afficheraient un instant dans la police du système.
void SplashScreen.preventAutoHideAsync();

export default function RootLayout() {
  // Fichiers de police inclus dans l'app : aucun appel réseau au lancement.
  const [fontsLoaded, fontError] = useFonts({
    Fredoka_600SemiBold,
    Fredoka_700Bold,
    Figtree_400Regular,
    Figtree_500Medium,
    Figtree_700Bold,
    Figtree_800ExtraBold,
  });
  // En cas d'échec, on continue avec la police du système plutôt que de rester bloqué.
  const fontsReady = fontsLoaded || fontError !== null;

  useEffect(() => {
    if (fontsReady) SplashScreen.hide();
  }, [fontsReady]);

  if (!fontsReady) {
    return null;
  }

  return (
    <ThemeProvider>
      <AuthProvider>
        <ThemedStatusBar />
        <RootNavigator />
      </AuthProvider>
    </ThemeProvider>
  );
}

/** Heure et batterie en foncé sur fond clair, en clair sur fond sombre. */
function ThemedStatusBar() {
  const theme = useTheme();
  return <StatusBar style={theme.scheme === 'dark' ? 'light' : 'dark'} />;
}

/**
 * Navigation protégée : les groupes (app) et (auth) ne sont accessibles que
 * si leur « guard » est vrai. Quand l'état de session change (connexion,
 * déconnexion, session expirée), Expo Router redirige automatiquement.
 */
function RootNavigator() {
  const { state, retry } = useAuth();
  // Rappels de péremption : reprogrammés si connecté, effacés à la déconnexion.
  useExpiryReminderSync(state);

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

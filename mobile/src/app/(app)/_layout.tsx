import { Stack } from 'expo-router';
import { useAuth } from '@/auth/AuthContext';
import { useReminderTaps } from '@/features/notifications/hooks';
import { useTheme } from '@/theme';

/**
 * Espace connecté. Tant que le profil n'existe pas (hasProfile false), seul
 * l'onboarding est accessible ; ensuite, les onglets et les écrans produit.
 */
export default function AppLayout() {
  const theme = useTheme();
  const { state } = useAuth();
  const hasProfile = state.status === 'signedIn' && state.user.hasProfile;
  // Appui sur un rappel de péremption : ouvre le Frigo (une fois l'onboarding terminé).
  useReminderTaps(hasProfile);

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
      <Stack.Protected guard={!hasProfile}>
        <Stack.Screen name="onboarding" options={{ headerShown: false }} />
      </Stack.Protected>
      <Stack.Protected guard={hasProfile}>
        <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
        <Stack.Screen name="item/new" options={{ title: 'Ajouter un produit', presentation: 'modal' }} />
        <Stack.Screen name="item/scan" options={{ title: 'Scanner un produit' }} />
        <Stack.Screen name="item/scanned" options={{ title: 'Nouveau produit' }} />
        <Stack.Screen name="item/[id]" options={{ title: 'Produit' }} />
        <Stack.Screen name="receipt/scan" options={{ title: 'Scanner un ticket' }} />
        <Stack.Screen name="receipt/review" options={{ title: 'Vérifier le ticket' }} />
        <Stack.Screen name="preferences" options={{ title: 'Mes préférences' }} />
        <Stack.Screen name="settings/theme" options={{ title: 'Thème' }} />
        <Stack.Screen name="settings/delete-account" options={{ title: 'Supprimer mon compte' }} />
        <Stack.Screen name="recipe/[id]" options={{ title: 'Recette' }} />
      </Stack.Protected>
    </Stack>
  );
}

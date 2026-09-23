import { Stack } from 'expo-router';
import { useAuth } from '@/auth/AuthContext';

/**
 * Espace connecté. Tant que le profil n'existe pas (hasProfile false), seul
 * l'onboarding est accessible ; ensuite, les onglets et les écrans produit.
 */
export default function AppLayout() {
  const { state } = useAuth();
  const hasProfile = state.status === 'signedIn' && state.user.hasProfile;

  return (
    <Stack>
      <Stack.Protected guard={!hasProfile}>
        <Stack.Screen name="onboarding" options={{ headerShown: false }} />
      </Stack.Protected>
      <Stack.Protected guard={hasProfile}>
        <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
        <Stack.Screen name="item/new" options={{ title: 'Ajouter un produit', presentation: 'modal' }} />
        <Stack.Screen name="item/scan" options={{ title: 'Scanner un produit' }} />
        <Stack.Screen name="item/scanned" options={{ title: 'Nouveau produit' }} />
        <Stack.Screen name="item/[id]" options={{ title: 'Produit' }} />
        <Stack.Screen name="preferences" options={{ title: 'Mes préférences' }} />
        <Stack.Screen name="recipe/[id]" options={{ title: 'Recette' }} />
      </Stack.Protected>
    </Stack>
  );
}

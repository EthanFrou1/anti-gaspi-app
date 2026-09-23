import { Stack } from 'expo-router';

/**
 * Espace connecté : les onglets (Frigo, Foyer, Compte), et par-dessus
 * les écrans d'ajout et de modification d'un produit.
 */
export default function AppLayout() {
  return (
    <Stack>
      <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
      <Stack.Screen name="item/new" options={{ title: 'Ajouter un produit', presentation: 'modal' }} />
      <Stack.Screen name="item/[id]" options={{ title: 'Produit' }} />
    </Stack>
  );
}

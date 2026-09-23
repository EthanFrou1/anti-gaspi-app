import { useState } from 'react';
import { StyleSheet, Text } from 'react-native';
import { useAuth } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { Screen } from '@/components/Screen';
import { colors } from '@/theme';

// Accueil provisoire : l'étape 5 le remplacera par les écrans du foyer.
export default function HomeScreen() {
  const { state, signOut } = useAuth();
  const [signingOut, setSigningOut] = useState(false);

  if (state.status !== 'signedIn') {
    return null;
  }

  async function handleSignOut() {
    setSigningOut(true);
    await signOut();
  }

  return (
    <Screen>
      <Text style={styles.greeting}>Bonjour {state.user.displayName} !</Text>
      <Text style={styles.text}>
        {state.user.householdId ? 'Tu fais partie d\'un foyer.' : 'Tu n\'as pas encore de foyer.'}
      </Text>
      <Button title="Se déconnecter" variant="secondary" onPress={() => void handleSignOut()} loading={signingOut} />
    </Screen>
  );
}

const styles = StyleSheet.create({
  greeting: { fontSize: 24, fontWeight: '700', color: colors.text },
  text: { fontSize: 16, color: colors.mutedText },
});

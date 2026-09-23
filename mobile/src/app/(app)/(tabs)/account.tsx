import { router } from 'expo-router';
import { useState } from 'react';
import { Alert, StyleSheet, Text, View } from 'react-native';
import { asApiError, type ApiError } from '@/api/errors';
import { useAuth } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { ErrorBanner } from '@/components/ErrorBanner';
import { Screen } from '@/components/Screen';
import { TextField } from '@/components/TextField';
import { colors, spacing } from '@/theme';

export default function AccountScreen() {
  const { state, signOut, deleteAccount } = useAuth();
  const [signingOut, setSigningOut] = useState(false);
  const [password, setPassword] = useState('');
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  if (state.status !== 'signedIn') {
    return null;
  }

  async function handleSignOut() {
    setSigningOut(true);
    await signOut();
  }

  function confirmDelete() {
    Alert.alert(
      'Supprimer ton compte ?',
      'Cette action est définitive. Si tu es propriétaire d\'un foyer, la propriété passera au membre ' +
        'le plus ancien ; si tu en es le seul membre, le foyer et son contenu seront supprimés.',
      [
        { text: 'Annuler', style: 'cancel' },
        { text: 'Supprimer', style: 'destructive', onPress: () => void handleDelete() },
      ],
    );
  }

  async function handleDelete() {
    setDeleting(true);
    setError(null);
    try {
      // En cas de succès, la navigation protégée renvoie vers l'écran de connexion.
      await deleteAccount(password);
    } catch (e) {
      setError(asApiError(e));
      setDeleting(false);
    }
  }

  return (
    <Screen hasHeader>
      <View style={styles.section}>
        <Text style={styles.name}>{state.user.displayName}</Text>
        <Text style={styles.email}>{state.user.email}</Text>
      </View>

      <Button title="Mes préférences alimentaires" onPress={() => router.push('/preferences')} />

      <Button title="Se déconnecter" variant="secondary" onPress={() => void handleSignOut()} loading={signingOut} />

      <View style={[styles.section, styles.dangerZone]}>
        <Text style={styles.title}>Supprimer mon compte</Text>
        <Text style={styles.text}>
          Toutes tes données personnelles seront effacées. Saisis ton mot de passe pour confirmer.
        </Text>
        <ErrorBanner message={error && !error.fieldError('Password') ? error.message : undefined} />
        <TextField
          label="Mot de passe"
          value={password}
          onChangeText={setPassword}
          error={error?.fieldError('Password')}
          secureTextEntry
          autoComplete="current-password"
          textContentType="password"
        />
        <Button
          title="Supprimer définitivement"
          onPress={confirmDelete}
          loading={deleting}
          disabled={password.length === 0}
        />
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  section: { gap: spacing.sm },
  name: { fontSize: 24, fontWeight: '700', color: colors.text },
  email: { fontSize: 16, color: colors.mutedText },
  dangerZone: {
    marginTop: spacing.xl,
    paddingTop: spacing.lg,
    borderTopWidth: StyleSheet.hairlineWidth,
    borderTopColor: colors.border,
  },
  title: { fontSize: 18, fontWeight: '700', color: colors.error },
  text: { fontSize: 14, color: colors.mutedText },
});

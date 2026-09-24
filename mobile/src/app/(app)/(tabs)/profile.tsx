import { router } from 'expo-router';
import { useState } from 'react';
import { Alert, Text, View } from 'react-native';
import { asApiError, type ApiError } from '@/api/errors';
import { useAuth } from '@/auth/AuthContext';
import { Avatar } from '@/components/Avatar';
import { Button } from '@/components/Button';
import { ErrorBanner } from '@/components/ErrorBanner';
import { Screen } from '@/components/Screen';
import { TextField } from '@/components/TextField';
import { makeStyles } from '@/theme';

export default function ProfileScreen() {
  const styles = useStyles();
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
      <View style={styles.header}>
        <Avatar userId={state.user.id} displayName={state.user.displayName} size={64} />
        <View style={styles.headerText}>
          <Text style={styles.name}>{state.user.displayName}</Text>
          <Text style={styles.email}>{state.user.email}</Text>
        </View>
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
          variant="danger"
          onPress={confirmDelete}
          loading={deleting}
          disabled={password.length === 0}
        />
      </View>
    </Screen>
  );
}

const useStyles = makeStyles((t) => ({
  header: { flexDirection: 'row', alignItems: 'center', gap: t.space.md, marginBottom: t.space.xs },
  headerText: { flex: 1, gap: 2 },
  section: { gap: t.space.sm },
  name: { ...t.type.title2, color: t.colors.ink },
  email: { ...t.type.callout, color: t.colors.ink3 },
  dangerZone: {
    marginTop: t.space.xl,
    paddingTop: t.space.xl,
    borderTopWidth: t.borderWidth.hairline,
    borderTopColor: t.colors.line,
  },
  title: { ...t.type.title3, color: t.colors.danger },
  text: { ...t.type.callout, color: t.colors.ink2 },
}));

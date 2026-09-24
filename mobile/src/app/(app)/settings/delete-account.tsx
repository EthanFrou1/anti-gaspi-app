import { useState } from 'react';
import { Alert, Text } from 'react-native';
import { asApiError, type ApiError } from '@/api/errors';
import { useAuth } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { ErrorBanner } from '@/components/ErrorBanner';
import { Screen } from '@/components/Screen';
import { TextField } from '@/components/TextField';
import { makeStyles } from '@/theme';

/**
 * Suppression du compte, sur un écran à part (comme dans les réglages d'iOS) : action
 * irréversible, confirmée par le mot de passe puis par une alerte.
 */
export default function DeleteAccountScreen() {
  const styles = useStyles();
  const { deleteAccount } = useAuth();
  const [password, setPassword] = useState('');
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

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
    </Screen>
  );
}

const useStyles = makeStyles((t) => ({
  text: { ...t.type.body, color: t.colors.ink2 },
}));

import { useState } from 'react';
import { StyleSheet, Text } from 'react-native';
import { asApiError, type ApiError } from '@/api/errors';
import { useAuth } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { ErrorBanner } from '@/components/ErrorBanner';
import { Screen } from '@/components/Screen';
import { TextField } from '@/components/TextField';
import { colors } from '@/theme';

// Doit rester aligné sur la règle de l'API (RequiredLength dans AddAppIdentity).
const MIN_PASSWORD_LENGTH = 10;

export default function RegisterScreen() {
  const { signUp } = useAuth();
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<ApiError | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit() {
    setSubmitting(true);
    setError(null);
    try {
      await signUp({ displayName: displayName.trim(), email: email.trim(), password });
    } catch (e) {
      setError(asApiError(e));
      setSubmitting(false);
    }
  }

  const hasFieldErrors = error !== null && Object.keys(error.fieldErrors).length > 0;

  return (
    <Screen hasHeader>
      <ErrorBanner message={error && !hasFieldErrors ? error.message : undefined} />

      <TextField
        label="Prénom ou pseudo"
        value={displayName}
        onChangeText={setDisplayName}
        error={error?.fieldError('DisplayName')}
        autoComplete="given-name"
        maxLength={50}
      />
      <TextField
        label="Email"
        value={email}
        onChangeText={setEmail}
        error={error?.fieldError('Email')}
        autoCapitalize="none"
        autoComplete="email"
        keyboardType="email-address"
        textContentType="emailAddress"
      />
      <TextField
        label="Mot de passe"
        value={password}
        onChangeText={setPassword}
        error={error?.fieldError('Password')}
        secureTextEntry
        autoComplete="new-password"
        textContentType="newPassword"
      />
      <Text style={styles.hint}>Au moins {MIN_PASSWORD_LENGTH} caractères. Une phrase courte, c'est parfait.</Text>

      <Button title="Créer mon compte" onPress={() => void handleSubmit()} loading={submitting} />
    </Screen>
  );
}

const styles = StyleSheet.create({
  hint: { fontSize: 13, color: colors.mutedText },
});

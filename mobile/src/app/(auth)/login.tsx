import { Link } from 'expo-router';
import { useState } from 'react';
import { StyleSheet, Text } from 'react-native';
import { asApiError, type ApiError } from '@/api/errors';
import { useAuth } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { ErrorBanner } from '@/components/ErrorBanner';
import { Screen } from '@/components/Screen';
import { TextField } from '@/components/TextField';
import { APP_NAME } from '@/config';
import { colors, spacing } from '@/theme';

export default function LoginScreen() {
  const { signIn } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<ApiError | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit() {
    setSubmitting(true);
    setError(null);
    try {
      // En cas de succès, la navigation protégée affiche l'app automatiquement.
      await signIn({ email: email.trim(), password });
    } catch (e) {
      setError(asApiError(e));
      setSubmitting(false);
    }
  }

  return (
    <Screen>
      <Text style={styles.title}>{APP_NAME}</Text>
      <Text style={styles.subtitle}>Connecte-toi pour retrouver ton frigo.</Text>

      <ErrorBanner message={error && Object.keys(error.fieldErrors).length === 0 ? error.message : undefined} />

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
        autoComplete="current-password"
        textContentType="password"
      />

      <Button title="Se connecter" onPress={() => void handleSubmit()} loading={submitting} />

      <Link href="/register" style={styles.link}>
        Pas encore de compte ? Inscris-toi
      </Link>
    </Screen>
  );
}

const styles = StyleSheet.create({
  title: { fontSize: 32, fontWeight: '700', color: colors.primary, marginTop: spacing.xl },
  subtitle: { fontSize: 16, color: colors.mutedText, marginBottom: spacing.md },
  link: { color: colors.primary, fontSize: 15, textAlign: 'center', padding: spacing.sm },
});

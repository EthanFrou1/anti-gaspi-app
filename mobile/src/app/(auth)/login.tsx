import { Link } from 'expo-router';
import { useState } from 'react';
import { Text, View } from 'react-native';
import { asApiError, type ApiError } from '@/api/errors';
import { useAuth } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { ErrorBanner } from '@/components/ErrorBanner';
import { Logo } from '@/components/Logo';
import { Screen } from '@/components/Screen';
import { TextField } from '@/components/TextField';
import { makeStyles } from '@/theme';

export default function LoginScreen() {
  const styles = useStyles();
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
      <View style={styles.header}>
        <Logo />
        <Text style={styles.subtitle}>Connecte-toi pour retrouver ton frigo.</Text>
      </View>

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

const useStyles = makeStyles((t) => ({
  header: { alignItems: 'center', gap: t.space.md, marginTop: t.space.xl, marginBottom: t.space.md },
  subtitle: { ...t.type.body, color: t.colors.ink2, textAlign: 'center' },
  link: { ...t.type.bodyBold, color: t.colors.primaryText, textAlign: 'center', padding: t.space.sm },
}));

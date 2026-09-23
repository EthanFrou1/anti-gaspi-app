import { useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { api } from '@/api/client';
import { asApiError, type ApiError } from '@/api/errors';
import { Button } from '@/components/Button';
import { ErrorBanner } from '@/components/ErrorBanner';
import { TextField } from '@/components/TextField';
import { colors, spacing } from '@/theme';

type Props = {
  // Appelé une fois le foyer créé ou rejoint.
  onDone: () => Promise<void>;
};

/**
 * Premier écran après l'inscription : l'inventaire appartient au foyer,
 * il faut donc en créer un ou en rejoindre un avant d'aller plus loin.
 */
export function NoHouseholdView({ onDone }: Props) {
  return (
    <View style={styles.container}>
      <Text style={styles.intro}>
        Ton frigo est partagé au sein d'un foyer : crée le tien, ou rejoins celui de ta coloc ou de ta famille.
      </Text>
      <CreateHouseholdForm onDone={onDone} />
      <Text style={styles.separator}>ou</Text>
      <JoinHouseholdForm onDone={onDone} />
    </View>
  );
}

function CreateHouseholdForm({ onDone }: Props) {
  const [name, setName] = useState('');
  const [error, setError] = useState<ApiError | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit() {
    setSubmitting(true);
    setError(null);
    try {
      await api.households.create(name.trim());
      await onDone();
    } catch (e) {
      setError(asApiError(e));
      setSubmitting(false);
    }
  }

  return (
    <View style={styles.section}>
      <Text style={styles.sectionTitle}>Créer un foyer</Text>
      <ErrorBanner message={error && !error.fieldError('Name') ? error.message : undefined} />
      <TextField
        label="Nom du foyer"
        placeholder="Ex. Appart rue Victor-Hugo"
        value={name}
        onChangeText={setName}
        error={error?.fieldError('Name')}
        maxLength={50}
      />
      <Button title="Créer" onPress={() => void handleSubmit()} loading={submitting} />
    </View>
  );
}

function JoinHouseholdForm({ onDone }: Props) {
  const [code, setCode] = useState('');
  const [error, setError] = useState<ApiError | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit() {
    setSubmitting(true);
    setError(null);
    try {
      // L'API tolère minuscules, espaces et tirets : pas besoin de nettoyer ici.
      await api.households.join(code);
      await onDone();
    } catch (e) {
      setError(asApiError(e));
      setSubmitting(false);
    }
  }

  return (
    <View style={styles.section}>
      <Text style={styles.sectionTitle}>Rejoindre un foyer</Text>
      <ErrorBanner message={error && !error.fieldError('Code') ? error.message : undefined} />
      <TextField
        label="Code d'invitation"
        placeholder="ABCD-2345"
        value={code}
        onChangeText={setCode}
        error={error?.fieldError('Code')}
        autoCapitalize="characters"
        autoCorrect={false}
        maxLength={20}
      />
      <Button title="Rejoindre" variant="secondary" onPress={() => void handleSubmit()} loading={submitting} />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { gap: spacing.lg },
  intro: { fontSize: 16, color: colors.mutedText },
  section: { gap: spacing.md },
  sectionTitle: { fontSize: 18, fontWeight: '700', color: colors.text },
  separator: { textAlign: 'center', color: colors.mutedText },
});

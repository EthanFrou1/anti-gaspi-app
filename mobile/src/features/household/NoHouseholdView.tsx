import { useState } from 'react';
import { Text, View } from 'react-native';
import { api } from '@/api/client';
import { asApiError, type ApiError } from '@/api/errors';
import { Button } from '@/components/Button';
import { ErrorBanner } from '@/components/ErrorBanner';
import { TextField } from '@/components/TextField';
import { makeStyles } from '@/theme';

type Props = {
  // Appelé une fois le foyer créé ou rejoint.
  onDone: () => Promise<void>;
};

/**
 * Premier écran après l'inscription : l'inventaire appartient au foyer,
 * il faut donc en créer un ou en rejoindre un avant d'aller plus loin.
 */
export function NoHouseholdView({ onDone }: Props) {
  const styles = useStyles();
  return (
    <View style={styles.container}>
      <Text style={styles.intro}>
        Ton frigo est partagé au sein d'un foyer : crée le tien, ou rejoins celui de ta coloc ou de ta famille.
      </Text>
      <CreateHouseholdForm onDone={onDone} />
      <View style={styles.separator}>
        <View style={styles.separatorLine} />
        <Text style={styles.separatorText}>ou</Text>
        <View style={styles.separatorLine} />
      </View>
      <JoinHouseholdForm onDone={onDone} />
    </View>
  );
}

function CreateHouseholdForm({ onDone }: Props) {
  const styles = useStyles();
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
  const styles = useStyles();
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

const useStyles = makeStyles((t) => ({
  container: { gap: t.space.xl },
  intro: { ...t.type.body, color: t.colors.ink2 },
  section: { gap: t.space.md },
  sectionTitle: { ...t.type.title3, color: t.colors.ink },
  separator: { flexDirection: 'row', alignItems: 'center', gap: t.space.sm },
  separatorLine: { flex: 1, height: t.borderWidth.hairline, backgroundColor: t.colors.line },
  separatorText: { ...t.type.callout, color: t.colors.ink3 },
}));

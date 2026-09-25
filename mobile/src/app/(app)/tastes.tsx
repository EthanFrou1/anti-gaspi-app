import { router } from 'expo-router';
import { useEffect, useState } from 'react';
import { ActivityIndicator, Text } from 'react-native';
import { api } from '@/api/client';
import { asApiError } from '@/api/errors';
import type { Profile } from '@/api/types';
import { Button } from '@/components/Button';
import { ErrorBanner } from '@/components/ErrorBanner';
import { Screen } from '@/components/Screen';
import { DislikesGrid } from '@/features/profile/DislikesGrid';
import { DEFAULT_PROFILE, toggle } from '@/features/profile/onboarding';
import { makeStyles, useTheme } from '@/theme';

/**
 * Profil → « Ce que je n'aime pas » : aliments « Pas pour moi » et réglage « Pas épicé ».
 * Privé comme le reste du profil ; les recettes n'en utilisent jamais quand tu es à table.
 */
export default function TastesScreen() {
  const theme = useTheme();
  const styles = useStyles();
  const [profile, setProfile] = useState<Profile | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    api.profile
      .get()
      .then((p) => setProfile(p ?? DEFAULT_PROFILE))
      .catch((e: unknown) => setError(asApiError(e).message));
  }, []);

  async function save() {
    if (!profile) return;
    setSaving(true);
    setError(null);
    try {
      await api.profile.save(profile);
      router.back();
    } catch (e) {
      setError(asApiError(e).message);
      setSaving(false);
    }
  }

  if (!profile) {
    return (
      <Screen hasHeader>
        {error ? <ErrorBanner message={error} /> : <ActivityIndicator style={styles.loader} size="large" color={theme.colors.primary} />}
      </Screen>
    );
  }

  return (
    <Screen hasHeader>
      <Text style={styles.intro}>
        Touche ce que tu n'aimes pas : tes recettes n'en contiendront jamais, même quand tu manges avec tes colocs.
      </Text>
      <DislikesGrid
        dislikes={profile.dislikes}
        avoidSpicy={profile.avoidSpicy}
        onToggleFood={(food) => setProfile({ ...profile, dislikes: toggle(profile.dislikes, food) })}
        onAvoidSpicyChange={(avoidSpicy) => setProfile({ ...profile, avoidSpicy })}
      />
      <ErrorBanner message={error ?? undefined} />
      <Button title="Enregistrer" onPress={() => void save()} loading={saving} />
      <Text style={styles.note}>Tes goûts restent privés : les autres membres du foyer ne les voient pas.</Text>
    </Screen>
  );
}

const useStyles = makeStyles((t) => ({
  loader: { marginTop: t.space['2xl'] },
  intro: { ...t.type.body, color: t.colors.ink2 },
  note: { ...t.type.caption, color: t.colors.ink3, textAlign: 'center' },
}));

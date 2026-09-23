import { router } from 'expo-router';
import { useEffect, useState } from 'react';
import { ActivityIndicator, StyleSheet, Text } from 'react-native';
import { api } from '@/api/client';
import { asApiError } from '@/api/errors';
import type { Profile } from '@/api/types';
import { Button } from '@/components/Button';
import { ErrorBanner } from '@/components/ErrorBanner';
import { Screen } from '@/components/Screen';
import { DEFAULT_PROFILE, validateProfile } from '@/features/profile/onboarding';
import {
  AllergiesSection,
  BudgetSection,
  CookingTimeSection,
  DietSection,
  GoalSection,
} from '@/features/profile/ProfileSections';
import { colors, spacing } from '@/theme';

/**
 * Toutes les préférences sur un seul écran, modifiables à tout moment
 * (mêmes blocs de questions que l'onboarding).
 */
export default function PreferencesScreen() {
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
    const problem = validateProfile(profile);
    if (problem) {
      setError(problem);
      return;
    }
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
        {error ? <ErrorBanner message={error} /> : <ActivityIndicator style={styles.loader} size="large" color={colors.primary} />}
      </Screen>
    );
  }

  const onChange = (next: Profile) => {
    setProfile(next);
    setError(null);
  };

  return (
    <Screen hasHeader>
      <Text style={styles.heading}>Régime</Text>
      <DietSection profile={profile} onChange={onChange} />

      <Text style={styles.heading}>Allergies</Text>
      <AllergiesSection profile={profile} onChange={onChange} />

      <Text style={styles.heading}>Temps de cuisine</Text>
      <CookingTimeSection profile={profile} onChange={onChange} />

      <Text style={styles.heading}>Budget par repas</Text>
      <BudgetSection profile={profile} onChange={onChange} />

      <Text style={styles.heading}>Objectif</Text>
      <GoalSection profile={profile} onChange={onChange} />

      <ErrorBanner message={error ?? undefined} />
      <Button title="Enregistrer" onPress={() => void save()} loading={saving} />
      <Text style={styles.note}>
        L'équipement de cuisine est commun au foyer : il se règle dans l'onglet Foyer.
      </Text>
    </Screen>
  );
}

const styles = StyleSheet.create({
  loader: { marginTop: spacing.xl },
  heading: { fontSize: 18, fontWeight: '700', color: colors.text, marginTop: spacing.md },
  note: { fontSize: 13, color: colors.mutedText, textAlign: 'center' },
});

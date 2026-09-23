import { useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { api } from '@/api/client';
import { asApiError } from '@/api/errors';
import type { Profile } from '@/api/types';
import { useAuth } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { ErrorBanner } from '@/components/ErrorBanner';
import { Screen } from '@/components/Screen';
import {
  AllergiesSection,
  BudgetSection,
  CookingTimeSection,
  DietSection,
  GoalSection,
} from '@/features/profile/ProfileSections';
import { DEFAULT_PROFILE, ONBOARDING_STEPS, skipStep, validateProfile, type OnboardingStepKey } from '@/features/profile/onboarding';
import { colors, spacing } from '@/theme';

const SECTIONS: Record<OnboardingStepKey, typeof DietSection> = {
  diet: DietSection,
  allergies: AllergiesSection,
  time: CookingTimeSection,
  budget: BudgetSection,
  goal: GoalSection,
};

/**
 * Les 5 questions posées après l'inscription. Les réponses restent dans un brouillon
 * local et sont envoyées en une seule fois à la fin. Chaque question peut être passée
 * (valeurs par défaut pensées pour les étudiants).
 */
export default function OnboardingScreen() {
  const { refreshUser, signOut } = useAuth();
  const [stepIndex, setStepIndex] = useState(0);
  const [draft, setDraft] = useState<Profile>(DEFAULT_PROFILE);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  // stepIndex reste toujours dans les bornes : ONBOARDING_STEPS[stepIndex] existe.
  const step = ONBOARDING_STEPS[stepIndex]!;
  const Section = SECTIONS[step.key];
  const isLast = stepIndex === ONBOARDING_STEPS.length - 1;

  async function next(profile: Profile) {
    const problem = validateProfile(profile);
    if (problem) {
      setError(problem);
      return;
    }
    setError(null);
    if (!isLast) {
      setStepIndex(stepIndex + 1);
      return;
    }

    setSaving(true);
    try {
      await api.profile.save(profile);
      // hasProfile devient vrai : la navigation protégée affiche l'app.
      await refreshUser();
    } catch (e) {
      setError(asApiError(e).message);
      setSaving(false);
    }
  }

  function skip() {
    const skipped = skipStep(draft, stepIndex);
    setDraft(skipped);
    void next(skipped);
  }

  return (
    <Screen>
      <Text style={styles.progress} accessibilityRole="progressbar">
        Question {stepIndex + 1} sur {ONBOARDING_STEPS.length}
      </Text>
      <View style={styles.progressBar}>
        <View style={[styles.progressFill, { width: `${((stepIndex + 1) / ONBOARDING_STEPS.length) * 100}%` }]} />
      </View>

      <Text style={styles.title}>{step.title}</Text>
      <Section profile={draft} onChange={(profile) => { setDraft(profile); setError(null); }} />

      <ErrorBanner message={error ?? undefined} />

      <View style={styles.actions}>
        <Button title={isLast ? 'Terminer' : 'Continuer'} onPress={() => void next(draft)} loading={saving} />
        <Button title="Passer cette question" variant="secondary" onPress={skip} disabled={saving} />
        {stepIndex > 0 ? (
          <Text style={styles.link} onPress={() => setStepIndex(stepIndex - 1)} accessibilityRole="button">
            ← Question précédente
          </Text>
        ) : (
          <Text style={styles.link} onPress={() => void signOut()} accessibilityRole="button">
            Se déconnecter
          </Text>
        )}
      </View>
      <Text style={styles.footnote}>Tu pourras tout modifier plus tard dans Compte → Mes préférences.</Text>
    </Screen>
  );
}

const styles = StyleSheet.create({
  progress: { fontSize: 13, color: colors.mutedText, marginTop: spacing.md },
  progressBar: { height: 6, borderRadius: 3, backgroundColor: colors.border, overflow: 'hidden' },
  progressFill: { height: 6, backgroundColor: colors.primary },
  title: { fontSize: 24, fontWeight: '700', color: colors.text, marginTop: spacing.sm },
  actions: { gap: spacing.sm, marginTop: spacing.md },
  link: { color: colors.primary, textAlign: 'center', padding: spacing.sm, fontSize: 15 },
  footnote: { fontSize: 12, color: colors.mutedText, textAlign: 'center' },
});

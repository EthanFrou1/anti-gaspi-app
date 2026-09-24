import { useState } from 'react';
import { Pressable, Text, View } from 'react-native';
import { api } from '@/api/client';
import { asApiError } from '@/api/errors';
import type { Profile } from '@/api/types';
import { useAuth } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { ErrorBanner } from '@/components/ErrorBanner';
import { Illustration } from '@/components/Illustration';
import { Screen } from '@/components/Screen';
import {
  AllergiesSection,
  BudgetSection,
  CookingTimeSection,
  DietSection,
  GoalSection,
} from '@/features/profile/ProfileSections';
import { DEFAULT_PROFILE, ONBOARDING_STEPS, skipStep, validateProfile, type OnboardingStepKey } from '@/features/profile/onboarding';
import { makeStyles } from '@/theme';

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
  const styles = useStyles();
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
      {/* La mascotte accueille l'utilisateur pendant les 5 questions. */}
      <View style={styles.mascot}>
        <Illustration name="mascotWelcoming" size={72} />
      </View>
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
          <Pressable onPress={() => setStepIndex(stepIndex - 1)} accessibilityRole="button" style={styles.linkButton}>
            <Text style={styles.link}>← Question précédente</Text>
          </Pressable>
        ) : (
          <Pressable onPress={() => void signOut()} accessibilityRole="button" style={styles.linkButton}>
            <Text style={styles.link}>Se déconnecter</Text>
          </Pressable>
        )}
      </View>
      <Text style={styles.footnote}>Tu pourras tout modifier plus tard dans Profil → Mes préférences.</Text>
    </Screen>
  );
}

const useStyles = makeStyles((t) => ({
  mascot: { alignItems: 'center', marginTop: t.space.md },
  progress: { ...t.type.overline, color: t.colors.ink3 },
  progressBar: { height: 8, borderRadius: t.radius.pill, backgroundColor: t.colors.surface2, overflow: 'hidden' },
  progressFill: { height: 8, borderRadius: t.radius.pill, backgroundColor: t.colors.primary },
  title: { ...t.type.title1, color: t.colors.ink, marginTop: t.space.xs },
  actions: { gap: t.space.sm, marginTop: t.space.md },
  linkButton: { minHeight: t.layout.minTouch, alignItems: 'center', justifyContent: 'center' },
  link: { ...t.type.bodyBold, color: t.colors.primaryText },
  footnote: { ...t.type.caption, color: t.colors.ink3, textAlign: 'center' },
}));

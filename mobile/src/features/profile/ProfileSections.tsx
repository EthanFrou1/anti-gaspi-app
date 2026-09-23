import { Pressable, StyleSheet, Switch, Text, View } from 'react-native';
import type { Profile } from '@/api/types';
import { ChoiceChips } from '@/components/ChoiceChips';
import { MultiChoiceChips } from '@/components/MultiChoiceChips';
import { colors, spacing } from '@/theme';
import {
  ALLERGEN_OPTIONS,
  BUDGET_OPTIONS,
  COOKING_TIME_OPTIONS,
  DIET_OPTIONS,
  EXCLUSION_OPTIONS,
  GOAL_OPTIONS,
} from './labels';
import { MAX_SERVINGS, MIN_SERVINGS, setHealthDataConsent, toggle } from './onboarding';

/**
 * Blocs de questions du profil, partagés par l'onboarding (un bloc par étape)
 * et l'écran « Mes préférences » (tous les blocs à la suite).
 */
type SectionProps = {
  profile: Profile;
  onChange: (profile: Profile) => void;
};

export function DietSection({ profile, onChange }: SectionProps) {
  return (
    <View style={styles.section}>
      <ChoiceChips options={DIET_OPTIONS} value={profile.diet} onChange={(diet) => onChange({ ...profile, diet })} />
      <Text style={styles.subtitle}>Ingrédients à éviter (facultatif)</Text>
      <MultiChoiceChips
        options={EXCLUSION_OPTIONS}
        values={profile.exclusions}
        onToggle={(value) => onChange({ ...profile, exclusions: toggle(profile.exclusions, value) })}
      />
    </View>
  );
}

export function AllergiesSection({ profile, onChange }: SectionProps) {
  return (
    <View style={styles.section}>
      <Text style={styles.help}>
        Les recettes proposées excluront ces allergènes. Vérifie toujours les étiquettes : l'app ne remplace pas
        un avis médical.
      </Text>
      <MultiChoiceChips
        options={ALLERGEN_OPTIONS}
        values={profile.allergens}
        onToggle={(value) => onChange({ ...profile, allergens: toggle(profile.allergens, value) })}
      />
      {/* Donnée de santé (RGPD, article 9) : consentement explicite, jamais coché d'avance. */}
      <View style={styles.consent}>
        <Switch
          value={profile.healthDataConsent}
          onValueChange={(consent) => onChange(setHealthDataConsent(profile, consent))}
          accessibilityLabel="Consentement à l'enregistrement de mes allergies"
        />
        <Text style={styles.consentText}>
          J'accepte que mes allergies soient enregistrées pour adapter les recettes. Elles ne sont jamais montrées aux
          autres membres du foyer. Je peux retirer ce consentement à tout moment : mes allergies seront alors effacées.
        </Text>
      </View>
    </View>
  );
}

export function CookingTimeSection({ profile, onChange }: SectionProps) {
  return (
    <View style={styles.section}>
      <ChoiceChips
        options={COOKING_TIME_OPTIONS}
        value={profile.cookingTime}
        onChange={(cookingTime) => onChange({ ...profile, cookingTime })}
      />
    </View>
  );
}

export function BudgetSection({ profile, onChange }: SectionProps) {
  return (
    <View style={styles.section}>
      <ChoiceChips options={BUDGET_OPTIONS} value={profile.budget} onChange={(budget) => onChange({ ...profile, budget })} />
    </View>
  );
}

export function GoalSection({ profile, onChange }: SectionProps) {
  const setServings = (value: number) =>
    onChange({ ...profile, defaultServings: Math.min(MAX_SERVINGS, Math.max(MIN_SERVINGS, value)) });

  return (
    <View style={styles.section}>
      <ChoiceChips options={GOAL_OPTIONS} value={profile.goal} onChange={(goal) => onChange({ ...profile, goal })} />
      <Text style={styles.subtitle}>Portions quand tu cuisines pour toi</Text>
      <View style={styles.stepper}>
        <StepperButton label="−" onPress={() => setServings(profile.defaultServings - 1)} disabled={profile.defaultServings <= MIN_SERVINGS} />
        <Text style={styles.stepperValue} accessibilityLabel={`${profile.defaultServings} portions`}>
          {profile.defaultServings}
        </Text>
        <StepperButton label="+" onPress={() => setServings(profile.defaultServings + 1)} disabled={profile.defaultServings >= MAX_SERVINGS} />
      </View>
      <Text style={styles.help}>Choisis 2 si tu aimes cuisiner une fois pour deux repas.</Text>
    </View>
  );
}

function StepperButton({ label, onPress, disabled }: { label: string; onPress: () => void; disabled: boolean }) {
  return (
    <Pressable
      onPress={onPress}
      disabled={disabled}
      accessibilityRole="button"
      accessibilityLabel={label === '+' ? 'Une portion de plus' : 'Une portion de moins'}
      style={[styles.stepperButton, disabled && styles.disabled]}
    >
      <Text style={styles.stepperButtonText}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  section: { gap: spacing.md },
  subtitle: { fontSize: 15, fontWeight: '600', color: colors.text, marginTop: spacing.sm },
  help: { fontSize: 13, color: colors.mutedText },
  consent: { flexDirection: 'row', gap: spacing.md, alignItems: 'flex-start', marginTop: spacing.sm },
  consentText: { flex: 1, fontSize: 13, color: colors.text },
  stepper: { flexDirection: 'row', alignItems: 'center', gap: spacing.lg },
  stepperButton: {
    width: 44,
    height: 44,
    borderRadius: 22,
    borderWidth: 1,
    borderColor: colors.primary,
    alignItems: 'center',
    justifyContent: 'center',
  },
  stepperButtonText: { fontSize: 22, color: colors.primary, fontWeight: '600' },
  stepperValue: { fontSize: 24, fontWeight: '700', color: colors.text, minWidth: 32, textAlign: 'center' },
  disabled: { opacity: 0.4 },
});

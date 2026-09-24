import { Pressable, Switch, Text, View } from 'react-native';
import type { Profile } from '@/api/types';
import { ChoiceChips } from '@/components/ChoiceChips';
import { MultiChoiceChips } from '@/components/MultiChoiceChips';
import { Minus, Plus } from '@/components/icons/lucide';
import { makeStyles, useTheme } from '@/theme';
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
  const styles = useStyles();
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
  const theme = useTheme();
  const styles = useStyles();
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
          trackColor={{ true: theme.colors.primary, false: theme.colors.line }}
          ios_backgroundColor={theme.colors.line}
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
  const styles = useStyles();
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
  const styles = useStyles();
  return (
    <View style={styles.section}>
      <ChoiceChips options={BUDGET_OPTIONS} value={profile.budget} onChange={(budget) => onChange({ ...profile, budget })} />
    </View>
  );
}

export function GoalSection({ profile, onChange }: SectionProps) {
  const styles = useStyles();
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
  const theme = useTheme();
  const styles = useStyles();
  return (
    <Pressable
      onPress={onPress}
      disabled={disabled}
      accessibilityRole="button"
      accessibilityLabel={label === '+' ? 'Une portion de plus' : 'Une portion de moins'}
      style={[styles.stepperButton, disabled && styles.disabled]}
    >
      {label === '+' ? (
        <Plus size={22} strokeWidth={2.5} color={theme.colors.ink} />
      ) : (
        <Minus size={22} strokeWidth={2.5} color={theme.colors.ink} />
      )}
    </Pressable>
  );
}

const useStyles = makeStyles((t) => ({
  section: { gap: t.space.md },
  subtitle: { ...t.type.bodyBold, color: t.colors.ink, marginTop: t.space.xs },
  help: { ...t.type.caption, color: t.colors.ink3 },
  consent: { flexDirection: 'row', gap: t.space.md, alignItems: 'flex-start', marginTop: t.space.xs },
  consentText: { ...t.type.callout, flex: 1, color: t.colors.ink2 },
  stepper: { flexDirection: 'row', alignItems: 'center', gap: t.space.xl },
  stepperButton: {
    width: t.layout.minTouch,
    height: t.layout.minTouch,
    borderRadius: t.layout.minTouch / 2,
    borderWidth: t.borderWidth.selected,
    borderColor: t.colors.border,
    backgroundColor: t.colors.surface,
    alignItems: 'center',
    justifyContent: 'center',
  },
  stepperValue: { ...t.type.title1, color: t.colors.ink, minWidth: 40, textAlign: 'center' },
  disabled: { opacity: 0.4 },
}));

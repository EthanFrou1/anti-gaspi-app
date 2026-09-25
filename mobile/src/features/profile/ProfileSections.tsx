import { Switch, Text, View } from 'react-native';
import type { Profile } from '@/api/types';
import { ChoiceChips } from '@/components/ChoiceChips';
import { MultiChoiceChips } from '@/components/MultiChoiceChips';
import { Stepper } from '@/components/Stepper';
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

  return (
    <View style={styles.section}>
      <ChoiceChips options={GOAL_OPTIONS} value={profile.goal} onChange={(goal) => onChange({ ...profile, goal })} />
      <Text style={styles.subtitle}>Portions quand tu cuisines pour toi</Text>
      <Stepper
        value={profile.defaultServings}
        min={MIN_SERVINGS}
        max={MAX_SERVINGS}
        onChange={(defaultServings) => onChange({ ...profile, defaultServings })}
        valueLabel={`${profile.defaultServings} portions`}
        incrementLabel="Une portion de plus"
        decrementLabel="Une portion de moins"
      />
      <Text style={styles.help}>Choisis 2 si tu aimes cuisiner une fois pour deux repas.</Text>
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  section: { gap: t.space.md },
  subtitle: { ...t.type.bodyBold, color: t.colors.ink, marginTop: t.space.xs },
  help: { ...t.type.caption, color: t.colors.ink3 },
  consent: { flexDirection: 'row', gap: t.space.md, alignItems: 'flex-start', marginTop: t.space.xs },
  consentText: { ...t.type.callout, flex: 1, color: t.colors.ink2 },
}));

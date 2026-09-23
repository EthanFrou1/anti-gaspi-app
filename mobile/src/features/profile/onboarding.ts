import type { Profile } from '@/api/types';

/**
 * Logique de l'onboarding, en fonctions pures (testables sans interface).
 */

// Valeurs par défaut pensées pour la cible de lancement (étudiants) : utilisées
// quand l'utilisateur passe une question.
export const DEFAULT_PROFILE: Profile = {
  diet: 'Omnivore',
  exclusions: [],
  allergens: [],
  healthDataConsent: false,
  cookingTime: 'Under30Minutes',
  budget: 'Under2Euros',
  goal: 'SimpleAntiWaste',
  defaultServings: 1,
};

export const MIN_SERVINGS = 1;
export const MAX_SERVINGS = 12;

// Les 5 questions, dans l'ordre. Chaque étape liste les champs qu'elle modifie :
// « Passer » les remet à leur valeur par défaut.
export const ONBOARDING_STEPS = [
  { key: 'diet', title: 'Tu manges…', fields: ['diet', 'exclusions'] },
  { key: 'allergies', title: 'Des allergies ?', fields: ['allergens', 'healthDataConsent'] },
  { key: 'time', title: 'Combien de temps pour cuisiner ?', fields: ['cookingTime'] },
  { key: 'budget', title: 'Ton budget par repas', fields: ['budget'] },
  { key: 'goal', title: 'Ton objectif', fields: ['goal', 'defaultServings'] },
] as const satisfies readonly { key: string; title: string; fields: readonly (keyof Profile)[] }[];

export type OnboardingStepKey = (typeof ONBOARDING_STEPS)[number]['key'];

/**
 * Erreur empêchant de continuer, ou null. Seul cas bloquant : des allergies cochées
 * sans consentement (l'API les refuserait, RGPD article 9).
 */
export function validateProfile(profile: Profile): string | null {
  if (profile.allergens.length > 0 && !profile.healthDataConsent) {
    return 'Coche la case de consentement pour que tes allergies soient enregistrées, ou décoche-les.';
  }
  if (profile.defaultServings < MIN_SERVINGS || profile.defaultServings > MAX_SERVINGS) {
    return `Le nombre de portions doit être compris entre ${MIN_SERVINGS} et ${MAX_SERVINGS}.`;
  }
  return null;
}

/** « Passer » : remet les champs de l'étape à leur valeur par défaut. */
export function skipStep(profile: Profile, stepIndex: number): Profile {
  const step = ONBOARDING_STEPS[stepIndex];
  if (!step) {
    return profile;
  }
  const reset: Partial<Profile> = {};
  for (const field of step.fields) {
    Object.assign(reset, { [field]: DEFAULT_PROFILE[field] });
  }
  return { ...profile, ...reset };
}

/** Ajoute la valeur si elle est absente, la retire sinon (sélection multiple). */
export function toggle<T>(values: readonly T[], value: T): T[] {
  return values.includes(value) ? values.filter((v) => v !== value) : [...values, value];
}

/**
 * Retirer le consentement efface les allergies : c'est aussi ce que fait l'API.
 * Le refléter tout de suite évite d'afficher des allergies qui ne seront pas enregistrées.
 */
export function setHealthDataConsent(profile: Profile, consent: boolean): Profile {
  return consent ? { ...profile, healthDataConsent: true } : { ...profile, healthDataConsent: false, allergens: [] };
}

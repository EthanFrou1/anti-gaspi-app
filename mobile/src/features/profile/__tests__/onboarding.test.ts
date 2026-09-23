// Types de Jest chargés ici seulement : TypeScript 6 ne les inclut plus automatiquement,
// et on évite ainsi de les exposer au code de l'app.
/// <reference types="jest" />

import type { Profile } from '@/api/types';
import {
  DEFAULT_PROFILE,
  ONBOARDING_STEPS,
  setHealthDataConsent,
  skipStep,
  toggle,
  validateProfile,
} from '../onboarding';

const custom: Profile = {
  diet: 'Vegan',
  exclusions: ['Alcohol'],
  allergens: ['Peanuts'],
  healthDataConsent: true,
  cookingTime: 'Under15Minutes',
  budget: 'NoLimit',
  goal: 'MuscleGain',
  defaultServings: 3,
};

describe('ONBOARDING_STEPS', () => {
  it('pose 5 questions qui couvrent tous les champs du profil, une seule fois chacun', () => {
    const fields = ONBOARDING_STEPS.flatMap((s) => [...s.fields]);

    expect(ONBOARDING_STEPS).toHaveLength(5);
    expect([...fields].sort()).toEqual(Object.keys(DEFAULT_PROFILE).sort());
  });
});

describe('validateProfile', () => {
  it('accepte le profil par défaut (tout passer est possible)', () => {
    expect(validateProfile(DEFAULT_PROFILE)).toBeNull();
  });

  it('bloque des allergies cochées sans consentement', () => {
    expect(validateProfile({ ...DEFAULT_PROFILE, allergens: ['Milk'] })).toMatch(/consentement/);
  });

  it('accepte des allergies avec consentement', () => {
    expect(validateProfile({ ...DEFAULT_PROFILE, allergens: ['Milk'], healthDataConsent: true })).toBeNull();
  });

  it.each([0, 13])('refuse %i portion(s)', (servings) => {
    expect(validateProfile({ ...DEFAULT_PROFILE, defaultServings: servings })).not.toBeNull();
  });
});

describe('skipStep', () => {
  it('remet seulement les champs de l\'étape passée à leur valeur par défaut', () => {
    const afterSkip = skipStep(custom, 0); // étape « Tu manges… »

    expect(afterSkip.diet).toBe('Omnivore');
    expect(afterSkip.exclusions).toEqual([]);
    expect(afterSkip.cookingTime).toBe('Under15Minutes'); // autre étape : inchangé
  });

  it('passer les allergies retire aussi le consentement', () => {
    const afterSkip = skipStep(custom, 1);

    expect(afterSkip.allergens).toEqual([]);
    expect(afterSkip.healthDataConsent).toBe(false);
  });
});

describe('setHealthDataConsent', () => {
  it('retirer le consentement efface les allergies (comme le fait l\'API)', () => {
    expect(setHealthDataConsent(custom, false)).toMatchObject({ healthDataConsent: false, allergens: [] });
  });

  it('donner son consentement conserve les allergies cochées', () => {
    const withAllergy = { ...DEFAULT_PROFILE, allergens: ['Sesame' as const] };

    expect(setHealthDataConsent(withAllergy, true)).toMatchObject({ healthDataConsent: true, allergens: ['Sesame'] });
  });
});

describe('toggle', () => {
  it('ajoute puis retire une valeur', () => {
    expect(toggle(['Pork'], 'Alcohol')).toEqual(['Pork', 'Alcohol']);
    expect(toggle(['Pork', 'Alcohol'], 'Pork')).toEqual(['Alcohol']);
  });
});

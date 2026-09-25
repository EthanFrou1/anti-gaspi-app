// Types de Jest chargés ici seulement : TypeScript 6 ne les inclut plus automatiquement,
// et on évite ainsi de les exposer au code de l'app.
/// <reference types="jest" />

import AsyncStorage from '@react-native-async-storage/async-storage';
import {
  allergyWarning,
  CONSTRAINT_EXCLUSION_NOTE,
  decodeRestrictions,
  encodeRestrictions,
  generatedRecipeParams,
  guestsLabel,
  loadLastDiners,
  maxGuests,
  MEAL_RESTRICTION_OPTIONS,
  restoreDiners,
  saveLastDiners,
} from '../meal';

beforeEach(async () => {
  await AsyncStorage.clear();
});

describe('invités', () => {
  it('10 invités au plus, et 12 personnes à table en tout', () => {
    expect(maxGuests(1)).toBe(10);
    expect(maxGuests(3)).toBe(9);
    expect(maxGuests(12)).toBe(0);
  });

  it('se comptent au singulier et au pluriel', () => {
    expect(guestsLabel(0)).toBe('0 invité');
    expect(guestsLabel(1)).toBe('1 invité');
    expect(guestsLabel(3)).toBe('3 invités');
  });
});

describe('contraintes pour ce repas', () => {
  it('proposent les 7 contraintes, dont « Sans lactose (aucun produit laitier) »', () => {
    expect(MEAL_RESTRICTION_OPTIONS.map((o) => o.value)).toEqual([
      'Vegetarian', 'NoPork', 'NotSpicy', 'NoTreeNuts', 'NoPeanuts', 'NoGluten', 'NoDairy',
    ]);
    expect(MEAL_RESTRICTION_OPTIONS.find((o) => o.value === 'NoDairy')?.label).toBe('Sans lactose (aucun produit laitier)');
  });

  it('renforcent l\'avertissement seulement pour un allergène', () => {
    expect(allergyWarning([])).toBeNull();
    expect(allergyWarning(['Vegetarian', 'NoPork', 'NotSpicy'])).toBeNull();
    expect(allergyWarning(['NoPeanuts'])).toMatch(/^« Sans arachides » demandé : .*ne te fie pas à cette recette\.$/);
    expect(allergyWarning(['NoGluten', 'Vegetarian', 'NoPeanuts'])).toMatch(/^« Sans arachides », « Sans gluten » demandés/);
  });

  it('ne passent à l\'écran de la recette que ce qui sert, par la navigation', () => {
    expect(generatedRecipeParams('r1', [], false)).toEqual({ id: 'r1' });
    // « Végétarien » seul : pas d'avertissement renforcé, donc rien à transmettre.
    expect(generatedRecipeParams('r1', ['Vegetarian'], false)).toEqual({ id: 'r1' });
    expect(generatedRecipeParams('r1', ['NoGluten', 'Vegetarian'], true)).toEqual({
      id: 'r1',
      restrictions: 'NoGluten,Vegetarian',
      constraintsNote: '1',
    });
  });

  it('la mention des produits écartés ne nomme ni produit ni contrainte', () => {
    expect(CONSTRAINT_EXCLUSION_NOTE).toBe(
      'Certains produits du frigo ont été écartés pour respecter les contraintes des convives.',
    );
  });

  it('passent par la navigation sans valeur inconnue', () => {
    expect(decodeRestrictions(encodeRestrictions(['NoGluten', 'NoDairy']))).toEqual(['NoGluten', 'NoDairy']);
    expect(decodeRestrictions('NoGluten,Inconnue')).toEqual(['NoGluten']);
    expect(decodeRestrictions(undefined)).toEqual([]);
  });
});

describe('mémoire des convives', () => {
  it('garde seulement les membres actuels, et moi par défaut', () => {
    expect(restoreDiners(null, ['alice', 'bob'], 'alice')).toEqual(['alice']);
    expect(restoreDiners(['alice', 'bob'], ['alice', 'bob'], 'alice')).toEqual(['alice', 'bob']);
    // Bob a quitté le foyer.
    expect(restoreDiners(['bob'], ['alice'], 'alice')).toEqual(['alice']);
  });

  it('est enregistrée par utilisateur et par foyer, sur ce téléphone', async () => {
    await saveLastDiners('alice', 'h1', ['alice', 'bob']);
    expect(await loadLastDiners('alice', 'h1')).toEqual(['alice', 'bob']);
    expect(await loadLastDiners('alice', 'h2')).toBeNull();
    expect(await loadLastDiners('bob', 'h1')).toBeNull();
  });

  it('ignore une valeur abîmée', async () => {
    await AsyncStorage.setItem('leftly.lastDiners.alice.h1', '{pas du json');
    expect(await loadLastDiners('alice', 'h1')).toBeNull();
  });
});

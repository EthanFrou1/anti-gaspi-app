// Types de Jest chargés ici seulement : TypeScript 6 ne les inclut plus automatiquement,
// et on évite ainsi de les exposer au code de l'app.
/// <reference types="jest" />

import type { Recipe } from '@/api/types';
import {
  favoriteLabel,
  formatPrepTime,
  formatRecipeForSharing,
  fridgeIngredients,
  quotaLabel,
  toggleDiner,
} from '../rules';

describe('formatRecipeForSharing', () => {
  it('produit un texte lisible : titre, infos, ingrédients, étapes numérotées, source', () => {
    const recipe: Recipe = {
      id: 'r1',
      title: 'Riz sauté à la courgette',
      prepMinutes: 20,
      servings: 2,
      ingredients: [
        { name: 'Courgette', quantity: '1', inventoryItemId: 'i1' },
        { name: 'Sel', quantity: '', inventoryItemId: null },
      ],
      steps: ['Cuire le riz.', 'Faire sauter la courgette.'],
      createdAt: '2026-09-23T12:00:00Z',
      isFavorite: false,
      favoriteCount: 0,
      excludedByPreferences: [],
    };

    expect(formatRecipeForSharing(recipe, 'Leftly')).toBe(
      [
        'Riz sauté à la courgette',
        '20 min · 2 portions',
        '',
        'Ingrédients :',
        '- Courgette : 1',
        '- Sel',
        '',
        'Préparation :',
        '1. Cuire le riz.',
        '2. Faire sauter la courgette.',
        '',
        'Recette anti-gaspi proposée par Leftly.',
      ].join('\n'),
    );
  });
});

describe('favoriteLabel', () => {
  it.each([
    [false, 0, 'Ajouter aux favoris'],
    [true, 1, 'Dans tes favoris'],
    [true, 3, 'Dans tes favoris (et ceux de 2 autres)'],
    [false, 1, 'Favori de 1 membre : ajouter aussi'],
  ])('étoile %s, %i au total → « %s »', (isFavorite, favoriteCount, expected) => {
    expect(favoriteLabel({ isFavorite, favoriteCount })).toBe(expected);
  });
});

const quota = (remaining: number) => ({ used: 3 - remaining, limit: 3, remaining, resetsAt: '2026-09-23T22:00:00Z' });

describe('quotaLabel', () => {
  it.each([
    [3, '3 recettes restantes aujourd\'hui'],
    [1, '1 recette restante aujourd\'hui'],
    [0, 'Plus de recette pour aujourd\'hui : reviens demain !'],
  ])('%i restante(s) → « %s »', (remaining, expected) => {
    expect(quotaLabel(quota(remaining))).toBe(expected);
  });
});

describe('toggleDiner', () => {
  it('ajoute puis retire un convive', () => {
    expect(toggleDiner(['me'], 'bob')).toEqual(['me', 'bob']);
    expect(toggleDiner(['me', 'bob'], 'me')).toEqual(['bob']);
  });

  it('ne laisse jamais la table vide', () => {
    expect(toggleDiner(['me'], 'me')).toEqual(['me']);
  });
});

describe('fridgeIngredients', () => {
  it('garde seulement les ingrédients qui viennent du frigo', () => {
    const recipe = {
      ingredients: [
        { name: 'Courgette', quantity: '1', inventoryItemId: 'i1' },
        { name: 'Sel', quantity: 'selon le goût', inventoryItemId: null },
      ],
    } as Recipe;

    expect(fridgeIngredients(recipe).map((i) => i.name)).toEqual(['Courgette']);
  });
});

describe('formatPrepTime', () => {
  it.each([
    [15, '15 min'],
    [60, '1 h'],
    [70, '1 h 10'],
    [125, '2 h 05'],
  ])('%i min → « %s »', (minutes, expected) => {
    expect(formatPrepTime(minutes)).toBe(expected);
  });
});

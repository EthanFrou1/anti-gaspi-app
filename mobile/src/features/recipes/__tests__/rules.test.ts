// Types de Jest chargés ici seulement : TypeScript 6 ne les inclut plus automatiquement,
// et on évite ainsi de les exposer au code de l'app.
/// <reference types="jest" />

import type { Recipe } from '@/api/types';
import { formatPrepTime, fridgeIngredients, quotaLabel, toggleDiner } from '../rules';

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

// Types de Jest chargés ici seulement : TypeScript 6 ne les inclut plus automatiquement,
// et on évite ainsi de les exposer au code de l'app.
/// <reference types="jest" />

import type { Category, InventoryItem } from '@/api/types';
import {
  canModifyItem,
  countByUrgency,
  estimateExpiry,
  expiryStatus,
  filterByUrgency,
  filterItems,
  formatQuantity,
  parseQuantity,
  presetQuantity,
  quantityAfterUnitChange,
} from '../rules';

const TODAY = '2026-09-23';

describe('expiryStatus', () => {
  it.each([
    ['2026-09-20', 'expired', 'Périmé depuis 3 j'],
    ['2026-09-22', 'expired', 'Périmé depuis hier'],
    ['2026-09-23', 'critical', 'À consommer aujourd\'hui'],
    ['2026-09-24', 'critical', 'À consommer demain'],
    ['2026-09-25', 'critical', 'Dans 2 j'],
    ['2026-09-30', 'soon', 'Dans 7 j'],
    ['2026-10-01', 'ok', 'Dans 8 j'],
  ])('DLC au %s : %s', (expiresOn, urgency, label) => {
    expect(expiryStatus(expiresOn, 'UseBy', TODAY)).toEqual({ urgency, label });
  });

  it('une DDM dépassée n\'est jamais « périmée » : elle est « à vérifier »', () => {
    const status = expiryStatus('2026-09-01', 'BestBefore', TODAY);

    expect(status.urgency).toBe('check');
    expect(status.label).not.toMatch(/périmé/i);
  });

  it('une DDM à venir suit les mêmes paliers qu\'une DLC', () => {
    expect(expiryStatus('2026-09-24', 'BestBefore', TODAY).urgency).toBe('critical');
  });
});

describe('estimateExpiry', () => {
  it('ajoute la durée de la catégorie à la date d\'achat', () => {
    const groundMeat: Category = { id: 1, code: 'ground-meat', name: 'Viande hachée', defaultShelfLifeDays: 1, expiryKind: 'UseBy' };

    expect(estimateExpiry(TODAY, groundMeat)).toBe('2026-09-24');
  });
});

describe('formatQuantity', () => {
  it.each([
    [4, 'Piece', '4 pièces'],
    [1, 'Piece', '1 pièce'],
    [500, 'Gram', '500 g'],
    [1.5, 'Kilogram', '1,5 kg'],
    [0.25, 'Liter', '0,25 l'],
  ] as const)('%s %s → « %s »', (quantity, unit, expected) => {
    // toLocaleString peut utiliser une espace insécable : on la normalise pour comparer.
    expect(formatQuantity(quantity, unit).replace(/ | /g, ' ')).toBe(expected);
  });
});

describe('parseQuantity', () => {
  it.each([
    ['4', 4],
    ['1,5', 1.5],
    ['1.5', 1.5],
    [' 250 ', 250],
    ['0,001', 0.001],
  ])('« %s » → %s', (input, expected) => {
    expect(parseQuantity(input)).toBe(expected);
  });

  it.each(['', 'abc', '0', '-1', '1,2345', '100001', '1,5,2'])('refuse « %s »', (input) => {
    expect(parseQuantity(input)).toBeNull();
  });
});

describe('presetQuantity', () => {
  it.each([
    ['3', 'Piece', 3],
    [' 5 ', 'Piece', 5],
    ['1,0', 'Piece', 1],
    ['250', 'Gram', 250],
    ['0,5', 'Kilogram', 0.5],
    ['1.5', 'Liter', 1.5],
    ['750', 'Milliliter', 750],
  ] as const)('« %s » %s correspond au bouton %d', (input, unit, expected) => {
    expect(presetQuantity(input, unit)).toBe(expected);
  });

  it.each([
    ['6', 'Piece'],
    ['0,5', 'Piece'],
    ['3', 'Gram'],
    ['0,612', 'Kilogram'],
    ['', 'Piece'],
    ['abc', 'Gram'],
  ] as const)('« %s » %s n\'est pas un bouton (champ « Autre »)', (input, unit) => {
    expect(presetQuantity(input, unit)).toBeNull();
  });
});

describe('quantityAfterUnitChange', () => {
  it('efface un bouton qui n\'existe pas dans la nouvelle unité (3 pièces ne deviennent pas 3 g)', () => {
    expect(quantityAfterUnitChange('3', 'Piece', 'Gram')).toBe('');
  });

  it('garde un bouton commun aux deux unités (1 pièce → 1 kg)', () => {
    expect(quantityAfterUnitChange('1', 'Piece', 'Kilogram')).toBe('1');
  });

  it('garde une valeur qui n\'était pas un bouton', () => {
    expect(quantityAfterUnitChange('0,612', 'Gram', 'Kilogram')).toBe('0,612');
  });
});

describe('droits et filtres', () => {
  const common = { ownerUserId: null } as InventoryItem;
  const mine = { ownerUserId: 'me' } as InventoryItem;
  const bobs = { ownerUserId: 'bob' } as InventoryItem;

  it('un produit commun ou perso à moi est modifiable, pas celui d\'un autre', () => {
    expect(canModifyItem(common, 'me')).toBe(true);
    expect(canModifyItem(mine, 'me')).toBe(true);
    expect(canModifyItem(bobs, 'me')).toBe(false);
  });

  it('filtre « commun » et « à moi »', () => {
    const items = [common, mine, bobs];

    expect(filterItems(items, 'all', 'me')).toHaveLength(3);
    expect(filterItems(items, 'common', 'me')).toEqual([common]);
    expect(filterItems(items, 'mine', 'me')).toEqual([mine]);
  });
});

describe('filtres par état', () => {
  const item = (expiresOn: string, expiryKind: InventoryItem['expiryKind']) => ({ expiresOn, expiryKind }) as InventoryItem;
  const steak = item('2026-09-22', 'UseBy'); // DLC dépassée : périmé
  const pasta = item('2026-09-20', 'BestBefore'); // DDM dépassée : à vérifier
  const milk = item('2026-09-24', 'UseBy'); // demain : urgent
  const yogurt = item('2026-09-27', 'UseBy'); // dans 4 j : bientôt
  const rice = item('2027-09-01', 'BestBefore'); // OK
  const items = [steak, pasta, milk, yogurt, rice, item('2026-09-23', 'BestBefore')];

  it('compte les produits de chaque état', () => {
    expect(countByUrgency(items, TODAY)).toEqual({ expired: 1, critical: 2, check: 1, soon: 1, ok: 1 });
  });

  it('garde seulement l\'état choisi, ou tout sans filtre', () => {
    expect(filterByUrgency(items, 'expired', TODAY)).toEqual([steak]);
    expect(filterByUrgency(items, 'check', TODAY)).toEqual([pasta]);
    expect(filterByUrgency(items, null, TODAY)).toBe(items);
  });

  it('une DDM dépassée n\'est jamais comptée comme périmée', () => {
    expect(filterByUrgency([pasta], 'expired', TODAY)).toEqual([]);
  });
});

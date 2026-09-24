import type { Category, ExpiryKind, InventoryItem, QuantityUnit } from '@/api/types';
import { addDays, daysBetween } from '@/utils/dates';

/**
 * Règles d'affichage de l'inventaire, en fonctions pures (testables sans interface).
 * Le serveur reste la référence : ici, on ne fait qu'informer l'utilisateur.
 */

export type Urgency = 'expired' | 'check' | 'critical' | 'soon' | 'ok';

export type ExpiryStatus = {
  urgency: Urgency;
  label: string;
};

/**
 * Urgence d'un produit selon sa date et son type de date :
 * - DLC dépassée : « périmé » (à ne pas consommer) ;
 * - DDM dépassée : « à vérifier » — encore consommable, on ne pousse JAMAIS à jeter
 *   (règle anti-gaspi du projet) ;
 * - sinon : critique (≤ 2 j), bientôt (≤ 7 j), ou OK.
 */
export function expiryStatus(expiresOn: string, kind: ExpiryKind, today: string): ExpiryStatus {
  const days = daysBetween(today, expiresOn);

  if (days < 0) {
    return kind === 'UseBy'
      ? { urgency: 'expired', label: days === -1 ? 'Périmé depuis hier' : `Périmé depuis ${-days} j` }
      : { urgency: 'check', label: 'Date dépassée : à vérifier (encore bon ?)' };
  }
  if (days === 0) {
    return { urgency: 'critical', label: 'À consommer aujourd\'hui' };
  }
  if (days === 1) {
    return { urgency: 'critical', label: 'À consommer demain' };
  }
  if (days <= 2) {
    return { urgency: 'critical', label: `Dans ${days} j` };
  }
  if (days <= 7) {
    return { urgency: 'soon', label: `Dans ${days} j` };
  }
  return { urgency: 'ok', label: `Dans ${days} j` };
}

/** Aperçu de la date estimée pendant la saisie (l'API refait le calcul, qui fait foi). */
export function estimateExpiry(purchasedOn: string, category: Category): string {
  return addDays(purchasedOn, category.defaultShelfLifeDays);
}

const UNIT_LABELS: Record<QuantityUnit, { singular: string; plural: string }> = {
  Piece: { singular: 'pièce', plural: 'pièces' },
  Gram: { singular: 'g', plural: 'g' },
  Kilogram: { singular: 'kg', plural: 'kg' },
  Milliliter: { singular: 'ml', plural: 'ml' },
  Liter: { singular: 'l', plural: 'l' },
};

export const UNITS: QuantityUnit[] = ['Piece', 'Gram', 'Kilogram', 'Milliliter', 'Liter'];

export function unitLabel(unit: QuantityUnit, quantity = 2): string {
  const labels = UNIT_LABELS[unit];
  return quantity > 1 ? labels.plural : labels.singular;
}

/** 4 + Piece → « 4 pièces » ; 1.5 + Kilogram → « 1,5 kg ». */
export function formatQuantity(quantity: number, unit: QuantityUnit): string {
  const number = quantity.toLocaleString('fr-FR', { maximumFractionDigits: 3 });
  return `${number} ${unitLabel(unit, quantity)}`;
}

/**
 * Lit une quantité saisie au clavier (virgule ou point). Renvoie null si invalide.
 * Mêmes bornes que l'API (0,001 à 100 000).
 */
export function parseQuantity(input: string): number | null {
  const normalized = input.trim().replace(/\s/g, '').replace(',', '.');
  if (!/^\d+(\.\d{1,3})?$/.test(normalized)) {
    return null;
  }
  const value = Number(normalized);
  return value >= 0.001 && value <= 100000 ? value : null;
}

/**
 * Quantités proposées en boutons selon l'unité (« 3 pains », « 250 g de crème », « 1 l de lait »),
 * calées sur les formats courants des emballages ; « Autre » pour le reste.
 */
export const QUANTITY_PRESETS: Record<QuantityUnit, readonly number[]> = {
  Piece: [1, 2, 3, 4, 5],
  Gram: [100, 200, 250, 500, 1000],
  Kilogram: [0.5, 1, 1.5, 2],
  Milliliter: [100, 200, 250, 500, 750],
  Liter: [0.5, 1, 1.5, 2],
};

/** Quantité au format du champ texte : 0.5 → « 0,5 ». */
export function toQuantityText(quantity: number): string {
  return String(quantity).replace('.', ',');
}

/** Bouton de quantité (pour cette unité) correspondant au texte saisi, ou null (autre valeur, ou saisie invalide). */
export function presetQuantity(input: string, unit: QuantityUnit): number | null {
  const value = parseQuantity(input);
  return value !== null && QUANTITY_PRESETS[unit].includes(value) ? value : null;
}

/**
 * Quantité à garder quand l'unité change alors qu'un bouton est sélectionné. Si ce bouton
 * n'existe pas dans la nouvelle unité, la sélection est effacée : « 3 pièces » ne doit pas
 * devenir « 3 g » sans que l'utilisateur le voie. Sinon (ex. 1 pièce → 1 kg), elle est gardée.
 */
export function quantityAfterUnitChange(input: string, from: QuantityUnit, to: QuantityUnit): string {
  return presetQuantity(input, from) !== null && presetQuantity(input, to) === null ? '' : input;
}

/** Un produit commun est modifiable par tout membre ; un produit perso, par son seul propriétaire. */
export function canModifyItem(item: Pick<InventoryItem, 'ownerUserId'>, myUserId: string): boolean {
  return item.ownerUserId === null || item.ownerUserId === myUserId;
}

export type InventoryFilter = 'all' | 'common' | 'mine';

export function filterItems(items: InventoryItem[], filter: InventoryFilter, myUserId: string): InventoryItem[] {
  switch (filter) {
    case 'common':
      return items.filter((i) => i.ownerUserId === null);
    case 'mine':
      return items.filter((i) => i.ownerUserId === myUserId);
    default:
      return items;
  }
}

/** Ordre des filtres par état : du plus pressant au plus tranquille. */
export const URGENCY_FILTERS: readonly Urgency[] = ['expired', 'critical', 'check', 'soon', 'ok'];

/** Nombre de produits par état (pour les compteurs des filtres). */
export function countByUrgency(items: InventoryItem[], today: string): Record<Urgency, number> {
  const counts: Record<Urgency, number> = { expired: 0, critical: 0, check: 0, soon: 0, ok: 0 };
  for (const item of items) {
    counts[expiryStatus(item.expiresOn, item.expiryKind, today).urgency] += 1;
  }
  return counts;
}

/** Produits d'un état donné ; null = tous les états. */
export function filterByUrgency(items: InventoryItem[], urgency: Urgency | null, today: string): InventoryItem[] {
  return urgency === null ? items : items.filter((i) => expiryStatus(i.expiresOn, i.expiryKind, today).urgency === urgency);
}

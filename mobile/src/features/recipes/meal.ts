import AsyncStorage from '@react-native-async-storage/async-storage';
import type { MealRestriction } from '@/api/types';

/**
 * « Qui mange ? » : convives du foyer, invités sans nom et contraintes pour ce repas.
 * Fonctions pures (testables sans interface), plus la mémoire locale des convives.
 */

export const MAX_GUESTS = 10;
// Même borne que l'API : 12 personnes au plus à table, invités compris.
export const MAX_PEOPLE = 12;

export const MEAL_RESTRICTION_OPTIONS: { value: MealRestriction; label: string }[] = [
  { value: 'Vegetarian', label: 'Végétarien' },
  { value: 'NoPork', label: 'Sans porc' },
  { value: 'NotSpicy', label: 'Pas épicé' },
  { value: 'NoTreeNuts', label: 'Sans fruits à coque' },
  { value: 'NoPeanuts', label: 'Sans arachides' },
  { value: 'NoGluten', label: 'Sans gluten' },
  { value: 'NoDairy', label: 'Sans lactose (aucun produit laitier)' },
];

// Contraintes d'allergène : elles renforcent l'avertissement sur les étiquettes.
const ALLERGEN_RESTRICTIONS: readonly MealRestriction[] = ['NoTreeNuts', 'NoPeanuts', 'NoGluten', 'NoDairy'];

/** Invités possibles avec ce nombre de convives du foyer (10 au plus, 12 personnes à table). */
export function maxGuests(dinerCount: number): number {
  return Math.max(0, Math.min(MAX_GUESTS, MAX_PEOPLE - dinerCount));
}

export function guestsLabel(count: number): string {
  return count <= 1 ? `${count} invité` : `${count} invités`;
}

/**
 * Avertissement renforcé quand une contrainte d'allergène est cochée : la recette et les
 * vérifications réduisent le risque sans le supprimer. null sinon.
 */
export function allergyWarning(restrictions: readonly MealRestriction[]): string | null {
  const allergens = MEAL_RESTRICTION_OPTIONS.filter(
    (o) => ALLERGEN_RESTRICTIONS.includes(o.value) && restrictions.includes(o.value),
  ).map((o) => `« ${o.label} »`);
  if (allergens.length === 0) {
    return null;
  }
  const asked = allergens.length === 1 ? 'demandé' : 'demandés';
  return (
    `${allergens.join(', ')} ${asked} : la recette et nos vérifications réduisent le risque sans le supprimer. ` +
    'Vérifie chaque étiquette (y compris « peut contenir des traces »). ' +
    'En cas d\'allergie sévère, ne te fie pas à cette recette.'
  );
}

/** Mention générique : ni les produits, ni la contrainte (allergène, exclusion…). */
export const CONSTRAINT_EXCLUSION_NOTE =
  'Certains produits du frigo ont été écartés pour respecter les contraintes des convives.';

/**
 * Paramètres de l'écran de la recette juste après sa génération : ce qui n'est jamais enregistré
 * (contraintes d'allergène du repas, mention des produits écartés) passe par la navigation.
 */
export function generatedRecipeParams(
  recipeId: string,
  restrictions: readonly MealRestriction[],
  productsExcludedByConstraints: boolean,
): { id: string; restrictions?: string; constraintsNote?: string } {
  return {
    id: recipeId,
    ...(allergyWarning(restrictions) ? { restrictions: encodeRestrictions(restrictions) } : {}),
    ...(productsExcludedByConstraints ? { constraintsNote: '1' } : {}),
  };
}

/** Passage par l'URL de l'écran de la recette (jamais enregistré) : « NoGluten,NoPeanuts ». */
export function encodeRestrictions(restrictions: readonly MealRestriction[]): string {
  return restrictions.join(',');
}

export function decodeRestrictions(value: string | string[] | undefined): MealRestriction[] {
  const text = Array.isArray(value) ? value[0] : value;
  const known = MEAL_RESTRICTION_OPTIONS.map((o) => o.value);
  return (text ?? '').split(',').filter((v): v is MealRestriction => (known as string[]).includes(v));
}

/**
 * Convives mémorisés, limités aux membres actuels (un membre parti disparaît). Moi par défaut,
 * et toujours au moins une personne à table.
 */
export function restoreDiners(saved: readonly string[] | null, memberIds: readonly string[], myUserId: string): string[] {
  const diners = (saved ?? []).filter((id) => memberIds.includes(id));
  return diners.length > 0 ? diners : [myUserId];
}

// ---------- Mémoire locale des convives (jamais les invités ni les contraintes) ----------

const storageKey = (userId: string, householdId: string) => `leftly.lastDiners.${userId}.${householdId}`;

export async function loadLastDiners(userId: string, householdId: string): Promise<string[] | null> {
  try {
    const stored = await AsyncStorage.getItem(storageKey(userId, householdId));
    const parsed: unknown = stored ? JSON.parse(stored) : null;
    return Array.isArray(parsed) && parsed.every((id) => typeof id === 'string') ? parsed : null;
  } catch {
    return null;
  }
}

export async function saveLastDiners(userId: string, householdId: string, diners: readonly string[]): Promise<void> {
  try {
    await AsyncStorage.setItem(storageKey(userId, householdId), JSON.stringify(diners));
  } catch {
    // Sans enregistrement, la sélection revient à « moi seul » : rien de bloquant.
  }
}

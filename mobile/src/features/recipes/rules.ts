import type { Recipe, RecipeIngredient, RecipeQuota } from '@/api/types';

/**
 * Règles d'affichage des recettes, en fonctions pures (testables sans interface).
 */

/** « 2 recettes restantes aujourd'hui », ou un message quand le quota est épuisé. */
export function quotaLabel(quota: RecipeQuota): string {
  if (quota.remaining <= 0) {
    return 'Plus de recette pour aujourd\'hui : reviens demain !';
  }
  return quota.remaining === 1
    ? '1 recette restante aujourd\'hui'
    : `${quota.remaining} recettes restantes aujourd'hui`;
}

/**
 * Sélection des convives : ajoute ou retire un membre, sans jamais laisser la liste vide
 * (il faut au moins une personne à table).
 */
export function toggleDiner(diners: readonly string[], userId: string): string[] {
  if (diners.includes(userId)) {
    return diners.length > 1 ? diners.filter((id) => id !== userId) : [...diners];
  }
  return [...diners, userId];
}

/** Ingrédients qui viennent du frigo du foyer (candidats à « J'ai cuisiné »). */
export function fridgeIngredients(recipe: Recipe): (RecipeIngredient & { inventoryItemId: string })[] {
  return recipe.ingredients.filter(
    (i): i is RecipeIngredient & { inventoryItemId: string } => i.inventoryItemId !== null,
  );
}

/** 15 → « 15 min » ; 70 → « 1 h 10 » ; 60 → « 1 h ». */
export function formatPrepTime(minutes: number): string {
  if (minutes < 60) {
    return `${minutes} min`;
  }
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  return rest === 0 ? `${hours} h` : `${hours} h ${String(rest).padStart(2, '0')}`;
}

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

/**
 * Recette en texte simple, pour la feuille de partage native (SMS, messagerie…).
 */
export function formatRecipeForSharing(recipe: Recipe, appName: string): string {
  const servings = `${recipe.servings} portion${recipe.servings > 1 ? 's' : ''}`;
  const ingredients = recipe.ingredients
    .map((i) => `- ${i.name}${i.quantity ? ` : ${i.quantity}` : ''}`)
    .join('\n');
  const steps = recipe.steps.map((step, index) => `${index + 1}. ${step}`).join('\n');

  return [
    recipe.title,
    `${formatPrepTime(recipe.prepMinutes)} · ${servings}`,
    '',
    'Ingrédients :',
    ingredients,
    '',
    'Préparation :',
    steps,
    '',
    `Recette anti-gaspi proposée par ${appName}.`,
  ].join('\n');
}

/** Libellé du bouton étoile : mon étoile, et celles des autres membres du foyer. */
export function favoriteLabel(recipe: Pick<Recipe, 'isFavorite' | 'favoriteCount'>): string {
  if (recipe.favoriteCount === 0) {
    return 'Ajouter aux favoris';
  }
  const others = recipe.favoriteCount - (recipe.isFavorite ? 1 : 0);
  if (recipe.isFavorite) {
    return others > 0 ? `Dans tes favoris (et ceux de ${others} autre${others > 1 ? 's' : ''})` : 'Dans tes favoris';
  }
  return `Favori de ${recipe.favoriteCount} membre${recipe.favoriteCount > 1 ? 's' : ''} : ajouter aussi`;
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

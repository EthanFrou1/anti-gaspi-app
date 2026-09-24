import { categoryFallback, type CategoryGroup } from './tokens';

/**
 * Groupe d'icône de chaque catégorie de l'app, par code (stable, voir CategorySeed.cs côté API).
 * La charte fournit 21 icônes de groupe ; nos 26 catégories s'y rattachent ici.
 * Une catégorie ajoutée plus tard sans correspondance prend l'icône « autre ».
 */
export const categoryGroupByCode: Readonly<Record<string, CategoryGroup>> = {
  'ground-meat': 'viande',
  'fresh-meat': 'viande',
  poultry: 'volaille',
  'fish-seafood': 'poisson',
  'cold-cuts': 'charcuterie',
  eggs: 'oeufs',
  'fresh-milk': 'lait',
  'uht-milk': 'lait',
  yogurts: 'yaourts-desserts',
  'fresh-cheese': 'fromage',
  'soft-cheese': 'fromage',
  'hard-cheese': 'fromage',
  butter: 'beurre-creme',
  cream: 'beurre-creme',
  fruits: 'fruits',
  vegetables: 'legumes',
  'leafy-greens': 'salade-herbes',
  bread: 'pain',
  'ready-meals': 'traiteur',
  leftovers: 'restes',
  frozen: 'surgeles',
  canned: 'conserves',
  'dry-goods': 'epicerie',
  condiments: 'sauces',
  drinks: 'boissons',
  other: 'autre',
};

export function categoryGroup(code: string | undefined): CategoryGroup {
  return (code && categoryGroupByCode[code]) || categoryFallback;
}

import type { DislikedFood } from '@/api/types';

/**
 * Aliments « Pas pour moi » : libellés et emojis, en fonctions pures (testables sans interface).
 * Les mots-clés qui vérifient les recettes sont côté API (FoodPreferences.cs).
 */

export type DislikeOption = { value: DislikedFood; label: string; emoji: string };

// Emojis provisoires, remplacés plus tard par des illustrations de la charte. Quand aucun emoji
// ne correspond vraiment (courgette, navet…), un emoji approchant : le nom reste toujours affiché.
const OPTIONS: Record<DislikedFood, Omit<DislikeOption, 'value'>> = {
  Mushrooms: { label: 'Champignons', emoji: '🍄' },
  Onion: { label: 'Oignon', emoji: '🧅' },
  Garlic: { label: 'Ail', emoji: '🧄' },
  Leek: { label: 'Poireau', emoji: '🥬' },
  BellPepper: { label: 'Poivron', emoji: '🫑' },
  Eggplant: { label: 'Aubergine', emoji: '🍆' },
  Zucchini: { label: 'Courgette', emoji: '🥒' },
  Cucumber: { label: 'Concombre', emoji: '🥒' },
  Broccoli: { label: 'Brocoli', emoji: '🥦' },
  Cauliflower: { label: 'Chou-fleur', emoji: '🥦' },
  BrusselsSprouts: { label: 'Choux de Bruxelles', emoji: '🥬' },
  Spinach: { label: 'Épinards', emoji: '🥬' },
  Beetroot: { label: 'Betterave', emoji: '🟣' },
  Celery: { label: 'Céleri', emoji: '🥬' },
  Fennel: { label: 'Fenouil', emoji: '🌱' },
  Endive: { label: 'Endive', emoji: '🥬' },
  Radish: { label: 'Radis', emoji: '🌱' },
  Turnip: { label: 'Navet', emoji: '⚪' },
  Peas: { label: 'Petits pois', emoji: '🫛' },
  Tomato: { label: 'Tomate', emoji: '🍅' },
  Avocado: { label: 'Avocat', emoji: '🥑' },
  Olives: { label: 'Olives', emoji: '🫒' },
  Pickles: { label: 'Cornichons', emoji: '🥒' },
  Coriander: { label: 'Coriandre', emoji: '🌿' },
  Fish: { label: 'Poisson', emoji: '🐟' },
  Lamb: { label: 'Agneau', emoji: '🐑' },
  BlueCheese: { label: 'Fromage bleu', emoji: '🧀' },
  GoatCheese: { label: 'Fromage de chèvre', emoji: '🐐' },
  Legumes: { label: 'Légumineuses', emoji: '🫘' },
  Tofu: { label: 'Tofu', emoji: '⬜' },
  Coconut: { label: 'Noix de coco', emoji: '🥥' },
  Raisins: { label: 'Raisins secs', emoji: '🍇' },
};

/** Les cartes, dans l'ordre d'affichage (celui de la liste de l'API). */
export const DISLIKE_OPTIONS: DislikeOption[] = (Object.keys(OPTIONS) as DislikedFood[]).map((value) => ({
  value,
  ...OPTIONS[value],
}));

export function dislikeLabel(food: DislikedFood): string {
  return OPTIONS[food].label;
}

/** Bulle de la mandarine : elle réagit au nombre d'aliments écartés. */
export function mascotMessage(count: number): string {
  if (count === 0) return 'Dis-moi ce que tu n\'aimes pas';
  if (count <= 2) return 'Noté, on évite !';
  return 'Pas de souci, il reste plein de choses à cuisiner';
}

export function dislikesCountLabel(count: number): string {
  if (count === 0) return 'Aucun aliment écarté';
  return count === 1 ? '1 aliment écarté' : `${count} aliments écartés`;
}

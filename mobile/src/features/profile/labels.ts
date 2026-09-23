import type {
  Allergen,
  CookingTime,
  Diet,
  IngredientExclusion,
  KitchenEquipment,
  MealBudget,
  NutritionGoal,
} from '@/api/types';

/**
 * Libellés affichés pour chaque valeur. Le type Record<Type, string> oblige à fournir
 * un libellé pour CHAQUE valeur : en ajouter une côté API sans la traduire ici fait
 * échouer la compilation TypeScript.
 */

type Option<T extends string> = { value: T; label: string };

function toOptions<T extends string>(labels: Record<T, string>): Option<T>[] {
  return (Object.keys(labels) as T[]).map((value) => ({ value, label: labels[value] }));
}

export const DIET_LABELS: Record<Diet, string> = {
  Omnivore: 'Je mange de tout',
  Flexitarian: 'Flexitarien (peu de viande)',
  Pescatarian: 'Pescétarien (poisson, pas de viande)',
  Vegetarian: 'Végétarien',
  Vegan: 'Végan',
};

export const EXCLUSION_LABELS: Record<IngredientExclusion, string> = {
  Pork: 'Sans porc',
  Beef: 'Sans bœuf',
  Offal: 'Sans abats',
  Seafood: 'Sans fruits de mer',
  Alcohol: 'Sans alcool',
};

export const ALLERGEN_LABELS: Record<Allergen, string> = {
  Gluten: 'Gluten',
  Crustaceans: 'Crustacés',
  Eggs: 'Œufs',
  Fish: 'Poisson',
  Peanuts: 'Arachides',
  Soybeans: 'Soja',
  Milk: 'Lait',
  TreeNuts: 'Fruits à coque',
  Celery: 'Céleri',
  Mustard: 'Moutarde',
  Sesame: 'Sésame',
  Sulphites: 'Sulfites',
  Lupin: 'Lupin',
  Molluscs: 'Mollusques',
};

export const COOKING_TIME_LABELS: Record<CookingTime, string> = {
  Under15Minutes: '15 min max',
  Under30Minutes: '30 min max',
  Under60Minutes: '1 h max',
  NoLimit: 'Pas de limite',
};

export const BUDGET_LABELS: Record<MealBudget, string> = {
  Under2Euros: 'Moins de 2 €',
  From2To4Euros: '2 à 4 €',
  From4To7Euros: '4 à 7 €',
  NoLimit: 'Pas de limite',
};

export const GOAL_LABELS: Record<NutritionGoal, string> = {
  Balanced: 'Manger équilibré',
  MuscleGain: 'Prendre du muscle',
  LightMeals: 'Manger léger',
  SimpleAntiWaste: 'Juste ne rien gaspiller',
};

export const EQUIPMENT_LABELS: Record<KitchenEquipment, string> = {
  Hob: 'Plaques',
  Oven: 'Four',
  Microwave: 'Micro-ondes',
  AirFryer: 'Air fryer',
  Blender: 'Blender / mixeur',
};

export const DIET_OPTIONS = toOptions(DIET_LABELS);
export const EXCLUSION_OPTIONS = toOptions(EXCLUSION_LABELS);
export const ALLERGEN_OPTIONS = toOptions(ALLERGEN_LABELS);
export const COOKING_TIME_OPTIONS = toOptions(COOKING_TIME_LABELS);
export const BUDGET_OPTIONS = toOptions(BUDGET_LABELS);
export const GOAL_OPTIONS = toOptions(GOAL_LABELS);
export const EQUIPMENT_OPTIONS = toOptions(EQUIPMENT_LABELS);

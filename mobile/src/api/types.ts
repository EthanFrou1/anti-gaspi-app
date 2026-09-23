/**
 * Types des échanges avec l'API : miroir des DTOs C# (api/src/Api/Dtos).
 * Les dates arrivent en chaînes ISO 8601.
 */

export type User = {
  id: string;
  email: string;
  displayName: string;
  // null tant que l'utilisateur n'a ni créé ni rejoint de foyer.
  householdId: string | null;
  // false tant que l'onboarding (préférences alimentaires) n'est pas terminé.
  hasProfile: boolean;
};

export type AuthResponse = {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: User;
};

export type RegisterRequest = {
  email: string;
  password: string;
  displayName: string;
};

export type LoginRequest = {
  email: string;
  password: string;
};

// Format d'erreur standard renvoyé par l'API (RFC 9457 ProblemDetails).
export type ProblemDetails = {
  title?: string;
  status?: number;
  code?: string;
  errors?: Record<string, string[]>;
};

export type HouseholdRole = 'Member' | 'Owner';

export type HouseholdMember = {
  userId: string;
  displayName: string;
  role: HouseholdRole;
  joinedAt: string;
};

export type Household = {
  id: string;
  name: string;
  createdAt: string;
  myRole: HouseholdRole;
  // Équipement de la cuisine commune (modifiable par tout membre).
  equipment: KitchenEquipment[];
  // Triés par ancienneté dans le foyer (le premier après le propriétaire hérite de la propriété).
  members: HouseholdMember[];
};

export type Invitation = {
  id: string;
  code: string;
  expiresAt: string;
  // null si l'auteur a supprimé son compte.
  createdByUserId: string | null;
};

// ---------- Inventaire ----------

// DLC (« à consommer jusqu'au ») ou DDM (« de préférence avant », encore consommable après).
export type ExpiryKind = 'UseBy' | 'BestBefore';

export type QuantityUnit = 'Piece' | 'Gram' | 'Kilogram' | 'Milliliter' | 'Liter';

export type InventoryItemStatus = 'Active' | 'Consumed' | 'Discarded';

export type Category = {
  id: number;
  code: string;
  name: string;
  defaultShelfLifeDays: number;
  expiryKind: ExpiryKind;
};

// Les dates « jour » (achat, péremption) circulent au format « AAAA-MM-JJ », sans heure.
export type InventoryItem = {
  id: string;
  name: string;
  categoryId: number;
  quantity: number;
  unit: QuantityUnit;
  purchasedOn: string;
  expiresOn: string;
  expiryIsEstimated: boolean;
  expiryKind: ExpiryKind;
  barcode: string | null;
  ownerUserId: string | null;
  ownerDisplayName: string | null;
  status: InventoryItemStatus;
  statusChangedAt: string | null;
  createdAt: string;
};

export type SaveInventoryItemRequest = {
  name: string;
  categoryId: number;
  quantity: number;
  unit: QuantityUnit;
  purchasedOn: string;
  // null = date estimée par l'API d'après la catégorie.
  expiresOn: string | null;
  barcode: string | null;
  isPersonal: boolean;
};

// Suggestion de pré-remplissage après un scan (données Open Food Facts, licence ODbL).
export type ProductSuggestion = {
  barcode: string;
  name: string | null;
  brand: string | null;
  categoryId: number | null;
  quantity: number | null;
  unit: QuantityUnit | null;
  // Affichée uniquement sur l'écran de confirmation du scan, jamais stockée.
  imageUrl: string | null;
  source: string;
};

// ---------- Profil (préférences alimentaires) ----------
// Listes fermées, miroir des énumérations de l'API (Entities/ProfileEnums.cs).

export type CookingTime = 'Under15Minutes' | 'Under30Minutes' | 'Under60Minutes' | 'NoLimit';

export type MealBudget = 'Under2Euros' | 'From2To4Euros' | 'From4To7Euros' | 'NoLimit';

export type Diet = 'Omnivore' | 'Flexitarian' | 'Pescatarian' | 'Vegetarian' | 'Vegan';

export type IngredientExclusion = 'Pork' | 'Beef' | 'Offal' | 'Seafood' | 'Alcohol';

// Les 14 allergènes à déclaration obligatoire en Europe.
export type Allergen =
  | 'Gluten'
  | 'Crustaceans'
  | 'Eggs'
  | 'Fish'
  | 'Peanuts'
  | 'Soybeans'
  | 'Milk'
  | 'TreeNuts'
  | 'Celery'
  | 'Mustard'
  | 'Sesame'
  | 'Sulphites'
  | 'Lupin'
  | 'Molluscs';

export type NutritionGoal = 'Balanced' | 'MuscleGain' | 'LightMeals' | 'SimpleAntiWaste';

export type KitchenEquipment = 'Hob' | 'Oven' | 'Microwave' | 'AirFryer' | 'Blender';

export type Profile = {
  cookingTime: CookingTime;
  budget: MealBudget;
  diet: Diet;
  exclusions: IngredientExclusion[];
  allergens: Allergen[];
  // Consentement explicite au stockage des allergies (donnée de santé).
  healthDataConsent: boolean;
  goal: NutritionGoal;
  defaultServings: number;
};

// ---------- Recettes (IA) ----------

export type GenerateRecipeRequest = {
  // Membres du foyer qui mangent ; null = moi seul.
  dinerUserIds: string[] | null;
  servings: number | null;
};

export type RecipeIngredient = {
  name: string;
  quantity: string;
  // Renseigné quand l'ingrédient vient du frigo du foyer.
  inventoryItemId: string | null;
};

export type Recipe = {
  id: string;
  title: string;
  prepMinutes: number;
  servings: number;
  ingredients: RecipeIngredient[];
  steps: string[];
  createdAt: string;
};

export type RecipeQuota = {
  used: number;
  limit: number;
  remaining: number;
  resetsAt: string;
};

// Prompt exact (outil de développement uniquement).
export type RecipePromptPreview = {
  model: string;
  maxOutputTokens: number;
  system: string;
  userContent: string;
  clipboardText: string;
};

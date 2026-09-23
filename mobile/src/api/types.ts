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

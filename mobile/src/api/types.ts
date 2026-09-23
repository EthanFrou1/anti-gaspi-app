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

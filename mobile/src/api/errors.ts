import type { ProblemDetails } from './types';

/**
 * Erreur renvoyée par le client API. Toutes les erreurs réseau et HTTP sont
 * converties dans ce format unique pour que les écrans n'aient qu'un cas à gérer.
 */
export class ApiError extends Error {
  // 0 = le serveur n'a pas pu être joint (pas de réseau, timeout…).
  readonly status: number;
  // Code métier stable renvoyé par l'API (ex. « auth.invalid_credentials »).
  readonly code: string | undefined;
  // Erreurs par champ, avec les noms des propriétés des DTOs (« Email », « Password »…).
  readonly fieldErrors: Record<string, string[]>;

  constructor(message: string, status: number, code?: string, fieldErrors: Record<string, string[]> = {}) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
    this.fieldErrors = fieldErrors;
  }

  static network(): ApiError {
    return new ApiError('Impossible de joindre le serveur. Vérifie ta connexion.', 0, 'network');
  }

  static fromProblem(status: number, problem: ProblemDetails | null): ApiError {
    const message = problem?.title ?? defaultMessage(status);
    return new ApiError(message, status, problem?.code, problem?.errors ?? {});
  }

  get isNetworkError(): boolean {
    return this.status === 0;
  }

  fieldError(field: string): string | undefined {
    return this.fieldErrors[field]?.[0];
  }
}

/**
 * Convertit n'importe quelle erreur attrapée en ApiError (utile dans les catch).
 */
export function asApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }
  return new ApiError('Une erreur inattendue est survenue.', -1, 'unexpected');
}

function defaultMessage(status: number): string {
  if (status === 429) {
    return 'Trop de tentatives. Réessaie dans une minute.';
  }
  if (status >= 500) {
    return 'Le serveur a rencontré un problème. Réessaie plus tard.';
  }
  return 'Une erreur est survenue.';
}

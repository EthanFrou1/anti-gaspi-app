import { API_URL } from '@/config';
import { tokenStorage } from '@/auth/tokenStorage';
import { ApiError } from './errors';
import type { AuthResponse, LoginRequest, ProblemDetails, RegisterRequest, User } from './types';

/**
 * Seul module de l'app qui parle à l'API (règle du projet).
 *
 * Gestion des jetons :
 * - l'access token (15 min) est gardé en mémoire et ajouté à chaque requête ;
 * - si l'API répond 401, on échange le refresh token contre une nouvelle paire,
 *   puis on rejoue la requête une seule fois ;
 * - si le refresh échoue aussi, la session est terminée : on prévient l'AuthContext.
 */

const REQUEST_TIMEOUT_MS = 15_000;

let accessToken: string | null = null;

// Refresh en cours, partagé : si plusieurs requêtes reçoivent un 401 en même temps,
// un seul appel à /refresh est fait. Indispensable, car l'API révoque toute la session
// si un même refresh token est présenté deux fois (détection de vol).
let refreshInFlight: Promise<AuthResponse | null> | null = null;

let onSessionExpired: (() => void) | null = null;

export function setSessionExpiredHandler(handler: (() => void) | null): void {
  onSessionExpired = handler;
}

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';

type RequestOptions = {
  method?: HttpMethod;
  body?: unknown;
  // false pour les endpoints publics (connexion, inscription…).
  authenticated?: boolean;
};

async function send(path: string, options: RequestOptions, token: string | null): Promise<Response> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);

  const headers: Record<string, string> = { Accept: 'application/json' };
  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json';
  }
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  try {
    return await fetch(`${API_URL}${path}`, {
      method: options.method ?? 'GET',
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      signal: controller.signal,
    });
  } catch {
    // Pas de réseau, serveur éteint, timeout…
    throw ApiError.network();
  } finally {
    clearTimeout(timeout);
  }
}

async function toApiError(response: Response): Promise<ApiError> {
  let problem: ProblemDetails | null = null;
  try {
    problem = (await response.json()) as ProblemDetails;
  } catch {
    // Réponse sans corps JSON (ex. 401 ou 429 bruts) : message par défaut.
  }
  return ApiError.fromProblem(response.status, problem);
}

async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const authenticated = options.authenticated ?? true;

  let response = await send(path, options, authenticated ? accessToken : null);

  if (response.status === 401 && authenticated) {
    const session = await refreshSession();
    if (!session) {
      onSessionExpired?.();
      throw await toApiError(response);
    }
    response = await send(path, options, accessToken);
  }

  if (!response.ok) {
    throw await toApiError(response);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

function refreshSession(): Promise<AuthResponse | null> {
  refreshInFlight ??= doRefresh().finally(() => {
    refreshInFlight = null;
  });
  return refreshInFlight;
}

async function doRefresh(): Promise<AuthResponse | null> {
  const refreshToken = await tokenStorage.getRefreshToken();
  if (!refreshToken) {
    return null;
  }

  const response = await send('/api/auth/refresh', { method: 'POST', body: { refreshToken } }, null);

  if (response.status === 401) {
    // Jeton expiré, révoqué ou volé : la session est définitivement terminée.
    await clearSession();
    return null;
  }

  if (!response.ok) {
    // 429 ou 500 : problème passager, on garde le refresh token pour réessayer.
    throw await toApiError(response);
  }

  const session = (await response.json()) as AuthResponse;
  await saveSession(session);
  return session;
}

async function saveSession(session: AuthResponse): Promise<void> {
  accessToken = session.accessToken;
  await tokenStorage.setRefreshToken(session.refreshToken);
}

async function clearSession(): Promise<void> {
  accessToken = null;
  await tokenStorage.clear();
}

export const api = {
  auth: {
    async register(body: RegisterRequest): Promise<User> {
      const session = await request<AuthResponse>('/api/auth/register', {
        method: 'POST',
        body,
        authenticated: false,
      });
      await saveSession(session);
      return session.user;
    },

    async login(body: LoginRequest): Promise<User> {
      const session = await request<AuthResponse>('/api/auth/login', {
        method: 'POST',
        body,
        authenticated: false,
      });
      await saveSession(session);
      return session.user;
    },

    /**
     * Au démarrage : reprend la session enregistrée, s'il y en a une.
     * Renvoie null si l'utilisateur doit se reconnecter.
     * Lève une ApiError si le serveur est injoignable (la session est conservée).
     */
    async restoreSession(): Promise<User | null> {
      const session = await refreshSession();
      return session?.user ?? null;
    },

    async logout(): Promise<void> {
      const refreshToken = await tokenStorage.getRefreshToken();
      // Déconnexion locale d'abord : même sans réseau, l'utilisateur est déconnecté.
      await clearSession();
      if (!refreshToken) {
        return;
      }
      try {
        await request<void>('/api/auth/logout', {
          method: 'POST',
          body: { refreshToken },
          authenticated: false,
        });
      } catch {
        // Sans importance : le jeton expirera de lui-même côté serveur.
      }
    },
  },

  me: {
    get(): Promise<User> {
      return request<User>('/api/me');
    },

    async delete(password: string): Promise<void> {
      await request<void>('/api/me', { method: 'DELETE', body: { password } });
      await clearSession();
    },
  },
};

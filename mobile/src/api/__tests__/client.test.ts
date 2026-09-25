// Types de Jest chargés ici seulement : TypeScript 6 ne les inclut plus automatiquement,
// et on évite ainsi de les exposer au code de l'app.
/// <reference types="jest" />

import type { AuthResponse, User } from '../types';

/**
 * Tests du client API. Le serveur est simulé par un faux `fetch`, et le stockage
 * sécurisé par un simple objet en mémoire.
 *
 * Le client garde un état interne (access token, refresh en cours) : chaque test
 * recharge donc le module à neuf pour partir d'un état vierge.
 */

const API_URL = 'http://api.test';

const user: User = { id: 'u1', email: 'alice@example.com', displayName: 'Alice', householdId: null, hasProfile: true };

function session(n: number): AuthResponse {
  return {
    accessToken: `access-${n}`,
    accessTokenExpiresAt: '2030-01-01T00:00:00Z',
    refreshToken: `refresh-${n}`,
    refreshTokenExpiresAt: '2030-01-01T00:00:00Z',
    user,
  };
}

// Réponse HTTP minimale : seuls status, ok et json() sont utilisés par le client.
function reply(status: number, body?: unknown): Response {
  return {
    status,
    ok: status >= 200 && status < 300,
    json: async () => {
      if (body === undefined) {
        throw new SyntaxError('Pas de corps JSON');
      }
      return body;
    },
  } as unknown as Response;
}

type Call = { path: string; method: string; authorization: string | undefined; body: unknown };
type Handler = (call: Call) => Response | Promise<Response>;

let storedRefreshToken: string | null;
let calls: Call[];

function setup(handler: Handler) {
  storedRefreshToken = null;
  calls = [];

  jest.resetModules();
  jest.doMock('@/config', () => ({ API_URL, APP_NAME: 'Test' }));
  jest.doMock('@/auth/tokenStorage', () => ({
    tokenStorage: {
      getRefreshToken: async () => storedRefreshToken,
      setRefreshToken: async (token: string) => {
        storedRefreshToken = token;
      },
      clear: async () => {
        storedRefreshToken = null;
      },
    },
  }));

  globalThis.fetch = jest.fn(async (url: string | URL | Request, init?: RequestInit) => {
    const headers = (init?.headers ?? {}) as Record<string, string>;
    const call: Call = {
      path: String(url).replace(API_URL, ''),
      method: init?.method ?? 'GET',
      authorization: headers.Authorization,
      body: typeof init?.body === 'string' ? JSON.parse(init.body) : undefined,
    };
    calls.push(call);
    return handler(call);
  }) as typeof fetch;

  // require (et non import) : charge le module APRÈS les mocks ci-dessus.
  // eslint-disable-next-line @typescript-eslint/no-require-imports
  const client = require('../client') as typeof import('../client');
  // eslint-disable-next-line @typescript-eslint/no-require-imports
  const { ApiError } = require('../errors') as typeof import('../errors');
  return { ...client, ApiError };
}

const refreshCalls = () => calls.filter((c) => c.path === '/api/auth/refresh');

describe('client API : session', () => {
  it('enregistre le refresh token à la connexion et envoie ensuite l\'access token', async () => {
    const { api } = setup(({ path }) => (path === '/api/auth/login' ? reply(200, session(1)) : reply(200, user)));

    await api.auth.login({ email: 'alice@example.com', password: 'secret-secret' });
    await api.me.get();

    expect(storedRefreshToken).toBe('refresh-1');
    expect(calls[1]).toMatchObject({ path: '/api/me', authorization: 'Bearer access-1' });
  });

  it('sur un 401, rafraîchit la session puis rejoue la requête', async () => {
    const { api } = setup(({ path, authorization }) => {
      if (path === '/api/auth/refresh') return reply(200, session(2));
      return authorization === 'Bearer access-2' ? reply(200, user) : reply(401);
    });
    storedRefreshToken = 'refresh-1';

    const me = await api.me.get();

    expect(me).toEqual(user);
    expect(refreshCalls()).toHaveLength(1);
    expect(refreshCalls()[0]?.body).toEqual({ refreshToken: 'refresh-1' });
    // Rotation : le nouveau refresh token remplace l'ancien.
    expect(storedRefreshToken).toBe('refresh-2');
  });

  it('plusieurs 401 simultanés ne déclenchent qu\'un seul refresh', async () => {
    const { api } = setup(async ({ path, authorization }) => {
      if (path === '/api/auth/refresh') {
        await new Promise((resolve) => setTimeout(resolve, 20));
        return reply(200, session(2));
      }
      return authorization === 'Bearer access-2' ? reply(200, user) : reply(401);
    });
    storedRefreshToken = 'refresh-1';

    await Promise.all([api.me.get(), api.me.get(), api.me.get()]);

    // Un second refresh avec le même jeton ferait couper toute la session par l'API.
    expect(refreshCalls()).toHaveLength(1);
  });

  it('si le refresh est refusé, efface la session et prévient l\'application', async () => {
    const { api, setSessionExpiredHandler, ApiError } = setup(() => reply(401));
    const onExpired = jest.fn();
    setSessionExpiredHandler(onExpired);
    storedRefreshToken = 'refresh-1';

    const error = await api.me.get().catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect(storedRefreshToken).toBeNull();
    expect(onExpired).toHaveBeenCalledTimes(1);
  });

  it('garde la session si le serveur est en erreur pendant le refresh', async () => {
    const { api, setSessionExpiredHandler } = setup(() => reply(500));
    const onExpired = jest.fn();
    setSessionExpiredHandler(onExpired);
    storedRefreshToken = 'refresh-1';

    await expect(api.auth.restoreSession()).rejects.toMatchObject({ status: 500 });

    expect(storedRefreshToken).toBe('refresh-1');
    expect(onExpired).not.toHaveBeenCalled();
  });

  it('garde la session et signale une erreur réseau si le serveur est injoignable', async () => {
    const { api } = setup(() => {
      throw new TypeError('Network request failed');
    });
    storedRefreshToken = 'refresh-1';

    const error = await api.auth.restoreSession().catch((e: unknown) => e);

    expect(error).toMatchObject({ isNetworkError: true });
    expect(storedRefreshToken).toBe('refresh-1');
  });

  it('ne contacte pas le serveur au démarrage s\'il n\'y a aucune session enregistrée', async () => {
    const { api } = setup(() => reply(500));

    await expect(api.auth.restoreSession()).resolves.toBeNull();

    expect(calls).toHaveLength(0);
  });

  it('déconnecte localement même si le serveur est injoignable', async () => {
    let online = true;
    const { api } = setup(({ path }) => {
      if (!online) throw new TypeError('Network request failed');
      return path === '/api/auth/login' ? reply(200, session(1)) : reply(401);
    });
    await api.auth.login({ email: 'alice@example.com', password: 'secret-secret' });
    online = false;

    await api.auth.logout();

    expect(storedRefreshToken).toBeNull();
  });
});

describe('client API : erreurs', () => {
  it('expose les erreurs de validation de l\'API champ par champ', async () => {
    const { api } = setup(() =>
      reply(400, {
        title: 'Les informations saisies sont invalides.',
        errors: { Email: ['L\'email n\'est pas valide.'] },
      }),
    );

    const error = await api.auth
      .register({ email: 'x', password: 'secret-secret', displayName: 'A' })
      .catch((e: unknown) => e);

    expect(error).toMatchObject({ status: 400, message: 'Les informations saisies sont invalides.' });
    expect((error as { fieldError: (f: string) => string | undefined }).fieldError('Email')).toBe(
      'L\'email n\'est pas valide.',
    );
  });

  it('donne un message par défaut quand la réponse n\'a pas de corps (ex. 429)', async () => {
    const { api } = setup(() => reply(429));

    await expect(api.auth.login({ email: 'a@b.c', password: 'x' })).rejects.toMatchObject({
      status: 429,
      message: 'Trop de tentatives. Réessaie dans une minute.',
    });
  });
});

describe('client API : foyer', () => {
  it('households.getMine renvoie null quand l\'utilisateur n\'a pas de foyer', async () => {
    const { api } = setup(() => reply(404, { title: 'Tu n\'as pas encore de foyer.', code: 'household.none' }));
    storedRefreshToken = null;

    await expect(api.households.getMine()).resolves.toBeNull();
  });

  it('products.lookupBarcode renvoie null pour un produit inconnu d\'Open Food Facts', async () => {
    const { api } = setup(() => reply(404, { title: 'Produit inconnu', code: 'product.not_found' }));

    await expect(api.products.lookupBarcode('3000000000017')).resolves.toBeNull();
  });

  it('products.lookupBarcode signale une recherche indisponible (503)', async () => {
    const { api } = setup(() => reply(503, { title: 'Indisponible', code: 'product.lookup_unavailable' }));

    await expect(api.products.lookupBarcode('3017620422003')).rejects.toMatchObject({
      status: 503,
      code: 'product.lookup_unavailable',
    });
  });

  it('recipes.generate laisse 60 s à l\'IA, les autres requêtes 15 s', async () => {
    const { api, RECIPE_GENERATION_TIMEOUT_MS } = setup(() => reply(201, { id: 'r1' }));
    const timeouts: number[] = [];
    const spy = jest.spyOn(globalThis, 'setTimeout').mockImplementation(((fn: () => void, ms?: number) => {
      timeouts.push(ms ?? 0);
      return 0 as unknown as ReturnType<typeof setTimeout>;
    }) as typeof setTimeout);
    try {
      await api.recipes.generate('h1', { dinerUserIds: null, servings: null });
      await api.recipes.quota();
    } finally {
      spy.mockRestore();
    }

    expect(RECIPE_GENERATION_TIMEOUT_MS).toBe(60_000);
    expect(timeouts).toEqual([60_000, 15_000]);
  });

  it('receipts.scan attend 75 s, au-delà du pire cas de l\'API (environ 58 s)', async () => {
    const { api, RECEIPT_SCAN_TIMEOUT_MS } = setup(() =>
      reply(200, { purchasedOn: '2026-09-25', purchaseDateFromReceipt: true, lines: [], skippedLineCount: 0 }),
    );
    const timeouts: number[] = [];
    const spy = jest.spyOn(globalThis, 'setTimeout').mockImplementation(((fn: () => void, ms?: number) => {
      timeouts.push(ms ?? 0);
      return 0 as unknown as ReturnType<typeof setTimeout>;
    }) as typeof setTimeout);
    try {
      await api.receipts.scan('h1', 'file:///ticket.jpg');
    } finally {
      spy.mockRestore();
    }

    expect(RECEIPT_SCAN_TIMEOUT_MS).toBe(75_000);
    expect(timeouts).toEqual([75_000]);
  });

  it('households.getMine propage les autres erreurs', async () => {
    const { api } = setup(() => reply(500));

    await expect(api.households.getMine()).rejects.toMatchObject({ status: 500 });
  });
});

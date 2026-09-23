import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api, setSessionExpiredHandler } from '@/api/client';
import { asApiError } from '@/api/errors';
import type { LoginRequest, RegisterRequest, User } from '@/api/types';

/**
 * État de session partagé par toute l'app. Union « discriminée » : TypeScript
 * sait que `user` n'existe que lorsque status vaut 'signedIn'.
 */
export type AuthState =
  | { status: 'loading' }
  | { status: 'unreachable'; message: string }
  | { status: 'signedOut' }
  | { status: 'signedIn'; user: User };

type AuthContextValue = {
  state: AuthState;
  signIn: (request: LoginRequest) => Promise<void>;
  signUp: (request: RegisterRequest) => Promise<void>;
  signOut: () => Promise<void>;
  // Relance la reprise de session (écran « serveur injoignable »).
  retry: () => Promise<void>;
  // Recharge l'utilisateur (ex. après avoir créé ou rejoint un foyer).
  refreshUser: () => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>({ status: 'loading' });

  const restore = useCallback(async () => {
    setState({ status: 'loading' });
    try {
      const user = await api.auth.restoreSession();
      setState(user ? { status: 'signedIn', user } : { status: 'signedOut' });
    } catch (error) {
      // Serveur injoignable ou en erreur : on garde la session et on propose de réessayer,
      // plutôt que de renvoyer l'utilisateur sur l'écran de connexion.
      setState({ status: 'unreachable', message: asApiError(error).message });
    }
  }, []);

  useEffect(() => {
    void restore();
  }, [restore]);

  // Le client API prévient ici quand la session a expiré en cours d'utilisation.
  useEffect(() => {
    setSessionExpiredHandler(() => setState({ status: 'signedOut' }));
    return () => setSessionExpiredHandler(null);
  }, []);

  const signIn = useCallback(async (request: LoginRequest) => {
    const user = await api.auth.login(request);
    setState({ status: 'signedIn', user });
  }, []);

  const signUp = useCallback(async (request: RegisterRequest) => {
    const user = await api.auth.register(request);
    setState({ status: 'signedIn', user });
  }, []);

  const signOut = useCallback(async () => {
    await api.auth.logout();
    setState({ status: 'signedOut' });
  }, []);

  const refreshUser = useCallback(async () => {
    const user = await api.me.get();
    setState({ status: 'signedIn', user });
  }, []);

  // useMemo : évite de re-rendre tous les consommateurs du contexte à chaque rendu.
  const value = useMemo(
    () => ({ state, signIn, signUp, signOut, retry: restore, refreshUser }),
    [state, signIn, signUp, signOut, restore, refreshUser],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth doit être utilisé à l\'intérieur de <AuthProvider>.');
  }
  return context;
}

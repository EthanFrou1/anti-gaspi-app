import * as SecureStore from 'expo-secure-store';

/**
 * Stockage du refresh token dans le stockage chiffré du système
 * (Keychain sur iOS, Keystore sur Android). Jamais dans AsyncStorage,
 * qui stocke en clair.
 *
 * L'access token, lui, reste uniquement en mémoire (voir api/client.ts) :
 * il ne vit que 15 minutes et se régénère à partir du refresh token.
 */
const REFRESH_TOKEN_KEY = 'auth.refreshToken';

export const tokenStorage = {
  getRefreshToken(): Promise<string | null> {
    return SecureStore.getItemAsync(REFRESH_TOKEN_KEY);
  },

  setRefreshToken(token: string): Promise<void> {
    return SecureStore.setItemAsync(REFRESH_TOKEN_KEY, token);
  },

  clear(): Promise<void> {
    return SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY);
  },
};

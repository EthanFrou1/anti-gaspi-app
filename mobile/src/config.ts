import Constants from 'expo-constants';

/**
 * Configuration centrale de l'app mobile.
 */

// Le nom vient de app.config.ts : on ne le recopie nulle part ailleurs.
export const APP_NAME = Constants.expoConfig?.name ?? 'App';

// Version affichée dans Profil → À propos (champ « version » de app.config.ts).
export const APP_VERSION = Constants.expoConfig?.version ?? '';

// Accès « statique » obligatoire (process.env.EXPO_PUBLIC_...) : Expo remplace
// cette expression par sa valeur au moment du build.
const apiUrl = process.env.EXPO_PUBLIC_API_URL;

if (!apiUrl) {
  throw new Error(
    'EXPO_PUBLIC_API_URL manquante : copie mobile/.env.example en mobile/.env.local puis relance Expo.',
  );
}

// Retire un éventuel « / » final pour pouvoir écrire `${API_URL}/api/...`.
export const API_URL = apiUrl.replace(/\/+$/, '');

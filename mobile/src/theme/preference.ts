import AsyncStorage from '@react-native-async-storage/async-storage';
import { Appearance } from 'react-native';

/** Choix de l'utilisateur dans Profil → Thème. « system » : suivre le réglage du téléphone. */
export type ThemePreference = 'system' | 'light' | 'dark';

export const THEME_PREFERENCES: readonly ThemePreference[] = ['system', 'light', 'dark'];

export const THEME_PREFERENCE_LABELS: Record<ThemePreference, string> = {
  system: 'Automatique',
  light: 'Clair',
  dark: 'Sombre',
};

// Réglage de l'appareil (pas une donnée personnelle) : stocké sur le téléphone seulement.
const STORAGE_KEY = 'leftly.themePreference';

/** Préférence enregistrée ; « system » si rien n'est enregistré, si la valeur est inconnue ou en cas d'erreur. */
export async function loadThemePreference(): Promise<ThemePreference> {
  try {
    const stored = await AsyncStorage.getItem(STORAGE_KEY);
    return isThemePreference(stored) ? stored : 'system';
  } catch {
    return 'system';
  }
}

export async function saveThemePreference(preference: ThemePreference): Promise<void> {
  try {
    await AsyncStorage.setItem(STORAGE_KEY, preference);
  } catch {
    // Sans enregistrement, le choix vaut pour la session en cours : rien de bloquant.
  }
}

/**
 * Applique le choix à TOUTE l'app, composants natifs compris (alertes, calendrier, clavier) :
 * useColorScheme() renvoie ensuite le mode choisi. « unspecified » rend la main au téléphone.
 */
export function applyThemePreference(preference: ThemePreference): void {
  Appearance.setColorScheme(preference === 'system' ? 'unspecified' : preference);
}

export function isThemePreference(value: unknown): value is ThemePreference {
  return typeof value === 'string' && (THEME_PREFERENCES as readonly string[]).includes(value);
}

import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';
import { StyleSheet, useColorScheme } from 'react-native';
import { applyThemePreference, saveThemePreference, type ThemePreference } from './preference';
import { darkTheme, lightTheme, type ColorScheme, type Theme } from './theme';

const ThemeContext = createContext<Theme>(lightTheme);

type PreferenceContextValue = {
  preference: ThemePreference;
  setPreference: (preference: ThemePreference) => void;
};

const PreferenceContext = createContext<PreferenceContextValue>({ preference: 'system', setPreference: () => undefined });

type Props = {
  children: ReactNode;
  // Choix enregistré, chargé (et déjà appliqué) pendant le splash : pas de changement de mode visible.
  initialPreference?: ThemePreference;
  // Mode imposé (tests, aperçus) ; sinon, celui de l'app (choix de l'utilisateur ou du téléphone).
  scheme?: ColorScheme;
};

/**
 * Fournit le thème de la charte, clair ou sombre. Le mode vient de useColorScheme(), qui suit
 * le téléphone, sauf si l'utilisateur a forcé Clair ou Sombre (voir applyThemePreference).
 */
export function ThemeProvider({ children, initialPreference = 'system', scheme }: Props) {
  const [preference, setPreferenceState] = useState(initialPreference);
  const system = useColorScheme();
  const resolved = scheme ?? (system === 'dark' ? 'dark' : 'light');

  const setPreference = useCallback((next: ThemePreference) => {
    // Appliqué tout de suite (l'écran change sans attendre) puis enregistré sur le téléphone.
    applyThemePreference(next);
    setPreferenceState(next);
    void saveThemePreference(next);
  }, []);

  const preferenceValue = useMemo(() => ({ preference, setPreference }), [preference, setPreference]);

  return (
    <PreferenceContext.Provider value={preferenceValue}>
      <ThemeContext.Provider value={resolved === 'dark' ? darkTheme : lightTheme}>{children}</ThemeContext.Provider>
    </PreferenceContext.Provider>
  );
}

/** Choix du thème (Automatique, Clair, Sombre) et moyen de le changer : écran Profil → Thème. */
export function useThemePreference(): PreferenceContextValue {
  return useContext(PreferenceContext);
}

export function useTheme(): Theme {
  return useContext(ThemeContext);
}

/**
 * Styles qui dépendent du thème. À déclarer hors du composant, comme StyleSheet.create :
 *
 *   const useStyles = makeStyles((t) => ({ title: { ...t.type.title2, color: t.colors.ink } }));
 *   function Screen() { const styles = useStyles(); … }
 *
 * Les styles sont créés une seule fois par mode (clair, sombre), puis réutilisés.
 */
export function makeStyles<T extends StyleSheet.NamedStyles<T>>(factory: (theme: Theme) => T): () => T {
  const cache = new Map<Theme, T>();
  return function useStyles(): T {
    const theme = useTheme();
    let styles = cache.get(theme);
    if (!styles) {
      styles = StyleSheet.create(factory(theme));
      cache.set(theme, styles);
    }
    return styles;
  };
}

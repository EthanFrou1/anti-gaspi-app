import { createContext, useContext, type ReactNode } from 'react';
import { StyleSheet, useColorScheme } from 'react-native';
import { darkTheme, lightTheme, type ColorScheme, type Theme } from './theme';

/**
 * TRANSITION : l'app reste en mode clair tant que tous les écrans ne sont pas migrés vers
 * useTheme(). Sinon, un écran pas encore migré resterait clair au milieu d'éléments sombres.
 * Passera à true (avec userInterfaceStyle « automatic » dans app.config.ts) à la fin de la migration.
 */
export const FOLLOW_SYSTEM_SCHEME = false;

const ThemeContext = createContext<Theme>(lightTheme);

type Props = {
  children: ReactNode;
  // Mode imposé (tests, aperçus) ; sinon, celui du téléphone.
  scheme?: ColorScheme;
};

/** Fournit le thème de la charte (clair ou sombre selon le réglage du téléphone). */
export function ThemeProvider({ children, scheme }: Props) {
  const system = useColorScheme();
  const resolved = scheme ?? (FOLLOW_SYSTEM_SCHEME && system === 'dark' ? 'dark' : 'light');
  return <ThemeContext.Provider value={resolved === 'dark' ? darkTheme : lightTheme}>{children}</ThemeContext.Provider>;
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

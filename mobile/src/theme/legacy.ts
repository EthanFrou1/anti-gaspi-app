import { colors as tokenColors, palette, space } from './tokens';

/**
 * TRANSITION vers la charte Leftly : anciennes valeurs statiques, encore utilisées par les
 * écrans pas encore migrés vers useTheme(). À supprimer une fois tous les écrans migrés.
 *
 * Les couleurs viennent maintenant de la charte (thème clair). « primary » servait à la fois
 * de fond et de couleur de texte : on prend la teinte « texte » de la mandarine, lisible dans
 * les deux cas (sur le fond crème, et avec du texte blanc par-dessus).
 */
const light = tokenColors.light;

export const colors = {
  primary: palette.tangerine.text,
  primaryText: '#FFFFFF',
  background: light.bg,
  text: light.ink,
  mutedText: light.ink3,
  border: light.line,
  error: light.danger,
  errorBackground: palette.framboise.soft,
};

// Mêmes valeurs qu'avant (4, 8, 16, 24, 32), tirées de la grille de 8 de la charte.
export const spacing = {
  xs: space.xxs,
  sm: space.xs,
  md: space.md,
  lg: space.xl,
  xl: space['2xl'],
};

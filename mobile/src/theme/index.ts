/**
 * Thème de l'app, d'après la charte Leftly (design/leftly/ et mobile/assets/brand/).
 * Écrans et composants : useTheme() et makeStyles(), qui suivent le mode clair ou sombre
 * du téléphone. Aucune couleur en dur dans les écrans.
 */
export { ThemeProvider, useTheme, makeStyles } from './ThemeProvider';
export { lightTheme, darkTheme, type Theme, type ThemeColors, type ColorScheme } from './theme';
export { categoryGroup, categoryGroupByCode } from './categories';
export { avatarColor, avatarInitial, AVATAR_TEXT_COLOR } from './avatar';
export { brandUrgency } from './urgency';
export type { Urgency as BrandUrgency, CategoryGroup } from './tokens';

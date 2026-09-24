/**
 * Thème de l'app, d'après la charte Leftly (design/leftly/ et mobile/assets/brand/).
 *
 * - Nouveaux écrans et composants : useTheme() et makeStyles(), qui suivent le mode clair
 *   ou sombre du téléphone.
 * - `colors` et `spacing` : valeurs statiques de TRANSITION pour les écrans pas encore migrés.
 */
export { ThemeProvider, useTheme, makeStyles, FOLLOW_SYSTEM_SCHEME } from './ThemeProvider';
export { lightTheme, darkTheme, type Theme, type ThemeColors, type ColorScheme } from './theme';
export { categoryGroup, categoryGroupByCode } from './categories';
export { avatarColor, avatarInitial, AVATAR_TEXT_COLOR } from './avatar';
export type { Urgency as BrandUrgency, CategoryGroup } from './tokens';
export { colors, spacing } from './legacy';

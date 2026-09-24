import type { ColorScheme } from './theme';
import { palette } from './tokens';

/**
 * Couleur de l'avatar d'un membre (badge « Perso », liste du foyer).
 * La charte donne des exemples par prénom (tokens `people`) : on ne peut pas coder de vrais
 * prénoms, la couleur est donc tirée des 5 couleurs vives de la palette.
 * Le texte de l'avatar est toujours à l'encre (lisible sur ces 5 couleurs, d'après la charte).
 */
const AVATAR_COLORS = ['tangerine', 'framboise', 'citron', 'raisin', 'myrtille'] as const;

/**
 * - Membres du foyer connus (ordre d'arrivée) : couleurs différentes pour les 5 premiers.
 * - Sinon : couleur tirée de l'identifiant, toujours la même pour un même membre.
 */
export function avatarColor(userId: string, scheme: ColorScheme, memberIds?: readonly string[]): string {
  const index = memberIds?.indexOf(userId) ?? -1;
  const slot = index >= 0 ? index % AVATAR_COLORS.length : hash(userId) % AVATAR_COLORS.length;
  const color = palette[AVATAR_COLORS[slot]!];
  return scheme === 'dark' ? color.dark : color.base;
}

/** Initiale affichée dans l'avatar (« Élodie » → « É »), « ? » si le nom est vide. */
export function avatarInitial(displayName: string | null | undefined): string {
  const first = displayName?.trim().charAt(0);
  return first ? first.toLocaleUpperCase('fr-FR') : '?';
}

export const AVATAR_TEXT_COLOR = palette.encre;

// Hachage simple et stable (le même identifiant donne toujours le même nombre).
function hash(value: string): number {
  let result = 0;
  for (let i = 0; i < value.length; i++) {
    result = (result * 31 + value.charCodeAt(i)) >>> 0;
  }
  return result;
}

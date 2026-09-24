import {
  borderWidth,
  colors,
  fonts,
  layout,
  motion,
  palette,
  persoBadge,
  radius,
  shadow,
  space,
  type,
  urgency,
  type Urgency,
} from './tokens';

export type ColorScheme = 'light' | 'dark';

export type ThemeColors = Record<keyof typeof colors.light, string>;

/**
 * Thème de l'app : les tokens de la charte, avec les couleurs du mode choisi (clair ou sombre)
 * déjà résolues. Les écrans n'ont ainsi jamais à tester le mode eux-mêmes.
 */
export type Theme = {
  scheme: ColorScheme;
  colors: ThemeColors;
  palette: typeof palette;
  // Badges d'urgence : fond + couleur de l'icône et du texte, libellé court, et couleur vive
  // (accent) pour le liseré des cartes du frigo.
  urgency: Record<Urgency, { bg: string; fg: string; accent: string; label: string }>;
  persoBadge: { bg: string; fg: string; avatarSize: number };
  type: typeof type;
  fonts: typeof fonts;
  space: typeof space;
  layout: typeof layout;
  radius: typeof radius;
  borderWidth: typeof borderWidth;
  shadow: typeof shadow;
  motion: typeof motion;
};

/**
 * Couleur de la palette associée à chaque urgence (adaptation propre à l'app). Les couleurs
 * du badge sont trop pâles (fond) ou trop sombres (texte) pour une bande fine : le liseré
 * prend la couleur vive correspondante.
 */
const URGENCY_ACCENT: Record<Urgency, 'framboise' | 'tangerine' | 'citron' | 'myrtille' | 'raisin'> = {
  expired: 'framboise',
  urgent: 'tangerine',
  soon: 'citron',
  ok: 'myrtille',
  check: 'raisin',
};

function buildTheme(scheme: ColorScheme): Theme {
  const urgencyColors = Object.fromEntries(
    Object.entries(urgency).map(([key, value]) => {
      const accent = palette[URGENCY_ACCENT[key as Urgency]];
      return [key, { ...value[scheme], accent: scheme === 'light' ? accent.base : accent.dark, label: value.label }];
    }),
  ) as Theme['urgency'];

  return {
    scheme,
    colors: colors[scheme],
    palette,
    urgency: urgencyColors,
    persoBadge: { bg: persoBadge.bg[scheme], fg: persoBadge.fg[scheme], avatarSize: persoBadge.avatarSize },
    type,
    fonts,
    space,
    layout,
    radius,
    borderWidth,
    shadow,
    motion,
  };
}

// Deux objets construits une fois pour toutes : un changement de mode ne recrée rien.
export const lightTheme = buildTheme('light');
export const darkTheme = buildTheme('dark');

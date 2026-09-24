/**
 * Leftly — design tokens (React Native / Expo)
 * Polices : npx expo install @expo-google-fonts/fredoka @expo-google-fonts/figtree
 * Titres : Fredoka (600/700) · Texte : Figtree (400–800)
 */

export const palette = {
  tangerine: { base: '#FF7A1A', text: '#C2410C', soft: '#FFE3CF', dark: '#FF8A33', darkText: '#FFA566', darkSoft: '#4A2614' },
  citron:    { base: '#FFD43B', text: '#735000', soft: '#FFF3B0', dark: '#FFD43B', darkText: '#FFE27A', darkSoft: '#3F340B' },
  raisin:    { base: '#8B5CF6', text: '#5B21B6', soft: '#EDE4FF', dark: '#A78BFA', darkText: '#D2BFFF', darkSoft: '#2F2152' },
  framboise: { base: '#F0457A', text: '#A3123E', soft: '#FFE0EA', dark: '#FF6B94', darkText: '#FFB8CC', darkSoft: '#4A1530' },
  myrtille:  { base: '#3D7BFF', text: '#1E44B8', soft: '#DCE7FF', dark: '#6B9BFF', darkText: '#B5CCFF', darkSoft: '#172A5E' },
  encre: '#1F1433', // texte sur toutes les couleurs vives (AA)
} as const;

const light = {
  bg: '#FFF8F3', surface: '#FFFFFF', surface2: '#FBEEE6', line: '#EADBD2',
  ink: '#1F1433', ink2: '#564A63', ink3: '#6B6079',
  border: '#1F1433', shadow: '#1F1433', buttonBorder: '#1F1433',
  primary: palette.tangerine.base, onPrimary: palette.encre, primaryText: palette.tangerine.text, primarySoft: palette.tangerine.soft,
  danger: '#BE123C', onDanger: '#FFFFFF',
  inverse: '#1F1433', onInverse: '#FFFFFF', scrim: 'rgba(31,20,51,0.55)',
};

const dark: typeof light = {
  bg: '#150F1E', surface: '#211A2D', surface2: '#2C2340', line: '#3A2F4C',
  ink: '#FFF4EC', ink2: '#D6CBE0', ink3: '#ADA1BB',
  border: '#4B3E60', shadow: '#07040C', buttonBorder: '#07040C',
  primary: palette.tangerine.dark, onPrimary: palette.encre, primaryText: palette.tangerine.darkText, primarySoft: palette.tangerine.darkSoft,
  danger: '#E11D48', onDanger: '#FFFFFF',
  inverse: '#FFF4EC', onInverse: '#150F1E', scrim: 'rgba(5,3,10,0.66)',
};

export const colors = { light, dark };

/** Système d'urgence : toujours fond + icône + libellé, jamais la couleur seule. */
export type Urgency = 'expired' | 'urgent' | 'soon' | 'ok' | 'check';
export const urgency: Record<Urgency, {
  label: string; icon: string; rule: string; mood: 'ko' | 'worried' | 'happy' | 'joy' | 'skeptic';
  light: { bg: string; fg: string }; dark: { bg: string; fg: string };
}> = {
  expired: { label: 'Périmé',     icon: 'x-octagon',    rule: 'DLC dépassée',                 mood: 'ko',      light: { bg: '#FFD6E2', fg: '#A3123E' }, dark: { bg: '#4A1530', fg: '#FFB8CC' } },
  urgent:  { label: 'Urgent',     icon: 'flame',        rule: '≤ 2 jours',                    mood: 'worried', light: { bg: '#FFDCC4', fg: '#A33A06' }, dark: { bg: '#4D2410', fg: '#FFB88A' } },
  soon:    { label: 'Bientôt',    icon: 'hourglass',    rule: '≤ 7 jours',                    mood: 'happy',   light: { bg: '#FFF0A8', fg: '#735000' }, dark: { bg: '#3F340B', fg: '#FFE27A' } },
  ok:      { label: 'OK',         icon: 'check-circle', rule: '> 7 jours',                    mood: 'joy',     light: { bg: '#D9E5FF', fg: '#1E44B8' }, dark: { bg: '#172A5E', fg: '#B5CCFF' } },
  check:   { label: 'À vérifier', icon: 'eye',          rule: 'DDM dépassée (jamais « périmé »)', mood: 'skeptic', light: { bg: '#EADFFF', fg: '#5B21B6' }, dark: { bg: '#2F2152', fg: '#D2BFFF' } },
};

export const fonts = {
  heading: 'Fredoka_700Bold',
  headingSemi: 'Fredoka_600SemiBold',
  body: 'Figtree_500Medium',
  bodyRegular: 'Figtree_400Regular',
  bodyBold: 'Figtree_700Bold',
  bodyHeavy: 'Figtree_800ExtraBold',
} as const;

/** Tailles en points : laisser allowFontScaling actif (texte agrandi du système). */
export const type = {
  display:  { fontFamily: fonts.heading,     fontSize: 40, lineHeight: 44 },
  title1:   { fontFamily: fonts.heading,     fontSize: 32, lineHeight: 38 },
  title2:   { fontFamily: fonts.heading,     fontSize: 24, lineHeight: 30 },
  title3:   { fontFamily: fonts.headingSemi, fontSize: 20, lineHeight: 26 },
  card:     { fontFamily: fonts.headingSemi, fontSize: 18, lineHeight: 22 },
  button:   { fontFamily: fonts.headingSemi, fontSize: 18, lineHeight: 22 },
  body:     { fontFamily: fonts.body,        fontSize: 17, lineHeight: 24 },
  bodyBold: { fontFamily: fonts.bodyBold,    fontSize: 17, lineHeight: 24 },
  callout:  { fontFamily: fonts.bodyBold,    fontSize: 15, lineHeight: 20 },
  caption:  { fontFamily: fonts.bodyBold,    fontSize: 13, lineHeight: 17 },
  overline: { fontFamily: fonts.bodyHeavy,   fontSize: 13, lineHeight: 16, letterSpacing: 0.8, textTransform: 'uppercase' as const },
} as const;

/** Grille de 8 */
export const space = { xxs: 4, xs: 8, sm: 12, md: 16, lg: 20, xl: 24, '2xl': 32, '3xl': 40, '4xl': 48 } as const;
export const layout = { screenPadding: 20, cardGap: 16, sectionGap: 24, minTouch: 44, tabBarHeight: 68, tabBarInset: 12, fabSize: 64, fabLift: 16 } as const;

export const radius = { sm: 12, md: 16, lg: 20, xl: 24, sheet: 28, pill: 999 } as const;

export const borderWidth = { hairline: 1.5, button: 2, selected: 2, fab: 2.5 } as const;

/**
 * Ombre « sticker » des boutons d'action : décalage net, sans flou.
 * iOS : shadowRadius 0 + shadowOpacity 1 fonctionne.
 * Android : elevation floute toujours → poser une View absolue décalée de `offsetY` derrière le bouton
 * (même rayon, couleur theme.shadow) pour garder le rendu net.
 */
export const shadow = {
  // uniquement pour les boutons d'action
  button: { offsetY: 4 },
  ios: (color: string, offsetY: number) => ({
    shadowColor: color, shadowOffset: { width: 0, height: offsetY }, shadowOpacity: 1, shadowRadius: 0,
  }),
} as const;

export const motion = {
  press: { translateY: 4, duration: 90 }, // bouton pressé : il « s'enfonce » dans son ombre
  sheet: { duration: 280, damping: 22 },
  celebrate: { duration: 800 },           // confettis « Produit sauvé »
} as const;

/** Badge « Perso · Prénom » : neutre (surface2 + ink2) + mini-avatar du membre. Jamais framboise (réservée à « Périmé »). */
export const persoBadge = { bg: { light: '#FBEEE6', dark: '#2C2340' }, fg: { light: '#564A63', dark: '#D6CBE0' }, avatarSize: 22 } as const;

/**
 * Groupes de catégories → icône (icons/categorie/*.svg, grille 24, trait 2 px, currentColor).
 * Les 27 catégories de l'app se rattachent chacune à l'un de ces 21 groupes ; « autre » sert de repli.
 * Les visages illustrés restent réservés aux moments forts.
 */
export const categoryGroups = {
  'viande': { label: 'Viande', icon: 'icons/categorie/viande.svg' },
  'volaille': { label: 'Volaille', icon: 'icons/categorie/volaille.svg' },
  'poisson': { label: 'Poisson & fruits de mer', icon: 'icons/categorie/poisson.svg' },
  'charcuterie': { label: 'Charcuterie', icon: 'icons/categorie/charcuterie.svg' },
  'oeufs': { label: 'Œufs', icon: 'icons/categorie/oeufs.svg' },
  'lait': { label: 'Lait', icon: 'icons/categorie/lait.svg' },
  'yaourts-desserts': { label: 'Yaourts & desserts', icon: 'icons/categorie/yaourts-desserts.svg' },
  'fromage': { label: 'Fromage', icon: 'icons/categorie/fromage.svg' },
  'beurre-creme': { label: 'Beurre & crème', icon: 'icons/categorie/beurre-creme.svg' },
  'fruits': { label: 'Fruits', icon: 'icons/categorie/fruits.svg' },
  'legumes': { label: 'Légumes', icon: 'icons/categorie/legumes.svg' },
  'salade-herbes': { label: 'Salade & herbes', icon: 'icons/categorie/salade-herbes.svg' },
  'pain': { label: 'Pain', icon: 'icons/categorie/pain.svg' },
  'traiteur': { label: 'Traiteur & plats préparés', icon: 'icons/categorie/traiteur.svg' },
  'restes': { label: 'Restes faits maison', icon: 'icons/categorie/restes.svg' },
  'surgeles': { label: 'Surgelés', icon: 'icons/categorie/surgeles.svg' },
  'conserves': { label: 'Conserves', icon: 'icons/categorie/conserves.svg' },
  'epicerie': { label: 'Épicerie sèche', icon: 'icons/categorie/epicerie.svg' },
  'sauces': { label: 'Sauces & condiments', icon: 'icons/categorie/sauces.svg' },
  'boissons': { label: 'Boissons', icon: 'icons/categorie/boissons.svg' },
  'autre': { label: 'Autre', icon: 'icons/categorie/autre.svg' },
} as const;
export type CategoryGroup = keyof typeof categoryGroups;
export const categoryFallback: CategoryGroup = 'autre';

/**
 * Style retenu : B « sobre ».
 * Cartes, listes, champs, barres, sheets : trait fin, aucune ombre.
 * L'ombre sticker (décalage net, sans flou) est réservée aux boutons d'action : primaire, secondaire, danger, bouton +.
 */
export const surfaces = {
  card:   { borderWidth: 1.5, borderColor: 'line', radius: 20, shadow: null },
  sheet:  { borderTopWidth: 1.5, borderColor: 'line', radius: 28, shadow: null },
  tabBar: { borderWidth: 1.5, borderColor: 'line', radius: 24, softShadow: { color: '#1F1433', opacity: 0.10, radius: 24, offsetY: 8 } },
  selected: { borderWidth: 2, borderColor: 'border' }, // sélection = bord plus épais + coche, jamais la couleur seule
  actionButton: { borderWidth: 2, borderColor: 'buttonBorder', stickerOffsetY: 4 },
  fab: { borderWidth: 2.5, borderColor: 'buttonBorder', stickerOffsetY: 4, size: 64 },
} as const;

/** Marque — logo retenu : V1 « tuile croquée » (tuile tangerine mordue dans le coin, L encre). Mascotte : mandarine-minuteur. */
export const brand = {
  name: 'Leftly',
  logo: {
    variant: 'V1-tuile-croquee',
    tile: palette.tangerine.base, glyph: palette.encre, tileRadius: 0.25, // rayon = 25 % du côté
    onTangerine: { tile: '#FFFFFF', glyph: palette.encre },           // version sur fond tangerine
    mono: { ink: palette.encre, white: '#FFFFFF' },
    minSize: 24,
  },
  appIcon: {
    ios: { background: '#FFF8F3', darkBackground: '#150F1E' },         // la bouchée laisse voir ce fond
    android: { background: '#FFF8F3', foregroundScale: 0.5 },          // tuile = 54 dp sur 108 dp (zone sûre Ø 66 dp)
  },
  splash: { light: { background: '#FFF8F3', text: palette.encre }, dark: { background: '#150F1E', text: '#FFF4EC' }, logoSize: 112 },
  mascot: { name: 'mandarine-minuteur', moods: ['ravie', 'accueillante', 'pressee'], usage: ['rappels', 'célébrations', 'onboarding'] },
} as const;

/** Icônes d'état (fichiers icons/etat/*.svg, trait currentColor 2 px, grille 24). */
export const stateIcons = { expired: 'x-octagon', urgent: 'flame', soon: 'hourglass', ok: 'check-circle', check: 'eye' } as const;

export const people = { Ethan: palette.tangerine.base, Alice: palette.framboise.base, Karim: palette.citron.base } as const;

export const theme = { light: { colors: light }, dark: { colors: dark }, type, space, radius, shadow, urgency } as const;
export default theme;

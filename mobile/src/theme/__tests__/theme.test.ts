// Types de Jest chargés ici seulement : TypeScript 6 ne les inclut plus automatiquement,
// et on évite ainsi de les exposer au code de l'app.
/// <reference types="jest" />
/// <reference types="node" />

import { existsSync } from 'fs';
import { join } from 'path';
import config, { BRAND_BACKGROUND } from '../../../app.config';
import { avatarColor, avatarInitial } from '../avatar';
import { categoryGroup, categoryGroupByCode } from '../categories';
import { darkTheme, lightTheme } from '../theme';
import { brand, categoryGroups, colors, palette, urgency } from '../tokens';

const MOBILE_ROOT = join(__dirname, '..', '..', '..');

// Codes des catégories de l'API (api/src/Api/Data/Seed/CategorySeed.cs) : à tenir à jour.
const API_CATEGORY_CODES = [
  'ground-meat', 'fresh-meat', 'poultry', 'fish-seafood', 'cold-cuts', 'eggs', 'fresh-milk', 'uht-milk',
  'yogurts', 'fresh-cheese', 'soft-cheese', 'hard-cheese', 'butter', 'cream', 'fruits', 'vegetables',
  'leafy-greens', 'bread', 'ready-meals', 'leftovers', 'frozen', 'canned', 'dry-goods', 'condiments',
  'drinks', 'other',
];

describe('catégories', () => {
  it('chaque catégorie de l\'API a son groupe d\'icône, sans retomber sur « autre » (sauf « other »)', () => {
    for (const code of API_CATEGORY_CODES) {
      expect(categoryGroupByCode[code]).toBeDefined();
    }
    expect(API_CATEGORY_CODES.filter((code) => categoryGroup(code) === 'autre')).toEqual(['other']);
  });

  it('une catégorie inconnue prend l\'icône « autre »', () => {
    expect(categoryGroup('nouvelle-categorie')).toBe('autre');
    expect(categoryGroup(undefined)).toBe('autre');
  });

  it('chaque groupe de la charte a son fichier SVG dans l\'app', () => {
    for (const group of Object.keys(categoryGroups)) {
      expect(existsSync(join(MOBILE_ROOT, 'assets', 'brand', 'icons', 'categorie', `${group}.svg`))).toBe(true);
    }
  });
});

describe('avatars', () => {
  it('un membre garde toujours la même couleur, prise dans la palette', () => {
    const color = avatarColor('3f2a9c1e-0000-4000-8000-000000000001', 'light');
    expect(avatarColor('3f2a9c1e-0000-4000-8000-000000000001', 'light')).toBe(color);
    expect([palette.tangerine, palette.framboise, palette.citron, palette.raisin, palette.myrtille].map((c) => c.base))
      .toContain(color);
  });

  it('les membres d\'un foyer ont des couleurs différentes (5 premiers), variantes sombres comprises', () => {
    const members = ['a', 'b', 'c', 'd', 'e'];
    const light = members.map((id) => avatarColor(id, 'light', members));
    expect(new Set(light).size).toBe(5);
    expect(avatarColor('a', 'dark', members)).toBe(palette.tangerine.dark);
  });

  it('initiale en majuscule, « ? » sans nom', () => {
    expect(avatarInitial(' élodie')).toBe('É');
    expect(avatarInitial('')).toBe('?');
    expect(avatarInitial(null)).toBe('?');
  });
});

describe('thèmes', () => {
  it('reprennent les couleurs de la charte pour chaque mode', () => {
    expect(lightTheme.colors).toEqual(colors.light);
    expect(darkTheme.colors).toEqual(colors.dark);
    expect(lightTheme.urgency.urgent).toEqual({ ...urgency.urgent.light, label: 'Urgent' });
    expect(darkTheme.urgency.check).toEqual({ ...urgency.check.dark, label: 'À vérifier' });
    expect(darkTheme.persoBadge.bg).toBe('#2C2340');
  });
});

describe('app.config.ts', () => {
  it('utilise les fonds de la charte pour l\'icône et le splash', () => {
    expect(BRAND_BACKGROUND.light).toBe(brand.splash.light.background);
    expect(BRAND_BACKGROUND.dark).toBe(brand.splash.dark.background);
    expect(BRAND_BACKGROUND.light).toBe(brand.appIcon.android.background);
  });

  it('ne pointe que vers des fichiers qui existent', () => {
    const paths = JSON.stringify(config).match(/\.\/assets\/[^"]+/g) ?? [];
    expect(paths.length).toBeGreaterThan(5);
    for (const path of paths) {
      expect({ path, exists: existsSync(join(MOBILE_ROOT, path)) }).toEqual({ path, exists: true });
    }
  });
});

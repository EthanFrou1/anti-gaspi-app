// Types de Jest chargés ici seulement : TypeScript 6 ne les inclut plus automatiquement,
// et on évite ainsi de les exposer au code de l'app.
/// <reference types="jest" />
/// <reference types="node" />

import { existsSync, readdirSync, readFileSync, statSync } from 'fs';
import { dirname, join, resolve } from 'path';

const SRC = join(__dirname, '..', '..');

function sourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) return name === '__tests__' ? [] : sourceFiles(path);
    return /\.tsx?$/.test(name) ? [path] : [];
  });
}

describe('fichiers de la charte', () => {
  it('chaque SVG importé par l\'app existe dans mobile/assets/brand/ (et pas seulement dans design/)', () => {
    const imports = sourceFiles(SRC).flatMap((file) =>
      [...readFileSync(file, 'utf-8').matchAll(/from '(\.[^']*assets\/brand\/[^']+\.svg)'/g)].map((match) => ({
        file,
        path: resolve(dirname(file), match[1]!),
      })),
    );

    // Icônes d'état et de catégorie, logo, illustrations.
    expect(imports.length).toBeGreaterThan(30);
    for (const { file, path } of imports) {
      expect({ file, path, exists: existsSync(path) }).toEqual({ file, path, exists: true });
    }
  });
});

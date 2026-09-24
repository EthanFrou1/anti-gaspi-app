// Types de Jest chargés ici seulement : TypeScript 6 ne les inclut plus automatiquement,
// et on évite ainsi de les exposer au code de l'app.
/// <reference types="jest" />

import { confettiPieces } from '../confetti';

const COLORS = ['#FF7A1A', '#FFD43B', '#8B5CF6'];

describe('confettis', () => {
  it('même tirage à chaque fois (animation prévisible)', () => {
    expect(confettiPieces(18, COLORS)).toEqual(confettiPieces(18, COLORS));
  });

  it('le nombre demandé, avec les couleurs de la palette en alternance', () => {
    const pieces = confettiPieces(6, COLORS);
    expect(pieces).toHaveLength(6);
    expect(pieces.map((p) => p.color)).toEqual([...COLORS, ...COLORS]);
  });

  it('dispersés autour du centre, sans s\'envoler hors de l\'écran', () => {
    for (const piece of confettiPieces(18, COLORS)) {
      const distance = Math.hypot(piece.dx, piece.dy + 40);
      expect(distance).toBeGreaterThanOrEqual(89);
      expect(distance).toBeLessThanOrEqual(161);
      expect(piece.size).toBeGreaterThanOrEqual(8);
      expect(piece.size).toBeLessThanOrEqual(14);
    }
  });
});

// Types de Jest chargés ici seulement : TypeScript 6 ne les inclut plus automatiquement,
// et on évite ainsi de les exposer au code de l'app.
/// <reference types="jest" />

import { DISLIKE_OPTIONS, dislikeLabel, dislikesCountLabel, mascotMessage } from '../dislikes';

describe('cartes « Pas pour moi »', () => {
  it('liste les 32 aliments de l\'API, chacun avec un nom et un emoji', () => {
    expect(DISLIKE_OPTIONS).toHaveLength(32);
    expect(new Set(DISLIKE_OPTIONS.map((o) => o.value)).size).toBe(32);
    for (const option of DISLIKE_OPTIONS) {
      expect(option.label.length).toBeGreaterThan(0);
      expect(option.emoji.length).toBeGreaterThan(0);
    }
  });

  it('distingue les petits pois des légumineuses', () => {
    expect(dislikeLabel('Peas')).toBe('Petits pois');
    expect(dislikeLabel('Legumes')).toBe('Légumineuses');
  });
});

describe('mandarine et compteur', () => {
  it('la mandarine réagit au nombre d\'aliments écartés', () => {
    expect(mascotMessage(0)).toBe('Dis-moi ce que tu n\'aimes pas');
    expect(mascotMessage(1)).toBe('Noté, on évite !');
    expect(mascotMessage(2)).toBe('Noté, on évite !');
    expect(mascotMessage(3)).toBe('Pas de souci, il reste plein de choses à cuisiner');
  });

  it('compte les aliments écartés', () => {
    expect(dislikesCountLabel(0)).toBe('Aucun aliment écarté');
    expect(dislikesCountLabel(1)).toBe('1 aliment écarté');
    expect(dislikesCountLabel(3)).toBe('3 aliments écartés');
  });
});

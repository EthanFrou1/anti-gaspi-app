// Types de Jest chargés ici seulement : TypeScript 6 ne les inclut plus automatiquement,
// et on évite ainsi de les exposer au code de l'app.
/// <reference types="jest" />

import { addDays, daysBetween, fromDateString, toLocalDateString } from '../dates';

describe('toLocalDateString', () => {
  it('utilise le calendrier local, pas UTC (23 h 30 reste « aujourd\'hui »)', () => {
    // Constructeur « local » : 23 septembre à 23 h 30, heure du téléphone.
    expect(toLocalDateString(new Date(2026, 8, 23, 23, 30))).toBe('2026-09-23');
  });

  it('complète les mois et jours sur deux chiffres', () => {
    expect(toLocalDateString(new Date(2026, 0, 5))).toBe('2026-01-05');
  });
});

describe('fromDateString', () => {
  it('donne minuit local du jour indiqué', () => {
    const date = fromDateString('2026-09-23');
    expect([date.getFullYear(), date.getMonth(), date.getDate(), date.getHours()]).toEqual([2026, 8, 23, 0]);
  });
});

describe('addDays', () => {
  it.each([
    ['2026-09-23', 3, '2026-09-26'],
    ['2026-09-29', 3, '2026-10-02'],
    ['2026-12-30', 7, '2027-01-06'],
    ['2028-02-27', 3, '2028-03-01'], // année bissextile
    ['2026-10-24', 2, '2026-10-26'], // passage à l'heure d'hiver
    ['2026-09-23', -1, '2026-09-22'],
  ])('%s + %i j = %s', (start, days, expected) => {
    expect(addDays(start, days)).toBe(expected);
  });
});

describe('daysBetween', () => {
  it.each([
    ['2026-09-23', '2026-09-23', 0],
    ['2026-09-23', '2026-09-26', 3],
    ['2026-09-23', '2026-09-20', -3],
    ['2026-03-28', '2026-03-30', 2], // passage à l'heure d'été : journée de 23 h
    ['2026-10-24', '2026-10-26', 2], // passage à l'heure d'hiver : journée de 25 h
  ])('de %s à %s : %i j', (from, to, expected) => {
    expect(daysBetween(from, to)).toBe(expected);
  });
});

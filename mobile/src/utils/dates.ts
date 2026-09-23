/**
 * Dates « jour » (sans heure) au format « AAAA-MM-JJ », comme dans l'API.
 *
 * Piège évité : new Date().toISOString() donne la date UTC. En France à 23 h 30,
 * ce serait déjà « demain ». On travaille donc sur le calendrier LOCAL du téléphone.
 */

const DAY_MS = 24 * 60 * 60 * 1000;

/** Date locale d'un instant (par défaut : maintenant). */
export function toLocalDateString(date: Date = new Date()): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/** « AAAA-MM-JJ » → Date à minuit LOCAL (pour le sélecteur de date natif). */
export function fromDateString(value: string): Date {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year ?? 1970, (month ?? 1) - 1, day ?? 1);
}

/** Ajoute (ou retire) des jours à une date « jour ». */
export function addDays(value: string, days: number): string {
  const date = fromDateString(value);
  date.setDate(date.getDate() + days);
  return toLocalDateString(date);
}

/**
 * Nombre de jours de `from` à `to` (négatif si `to` est passé).
 * Calcul en UTC pour ne pas être faussé par les changements d'heure (journées de 23 h ou 25 h).
 */
export function daysBetween(from: string, to: string): number {
  return Math.round((toUtcMidnight(to) - toUtcMidnight(from)) / DAY_MS);
}

/** « 2026-09-23 » → « 23 sept. » (ou « 23 sept. 2027 » si ce n'est pas l'année en cours). */
export function formatShortDate(value: string, today: string = toLocalDateString()): string {
  const date = fromDateString(value);
  const sameYear = value.slice(0, 4) === today.slice(0, 4);
  return date.toLocaleDateString('fr-FR', {
    day: 'numeric',
    month: 'short',
    ...(sameYear ? {} : { year: 'numeric' }),
  });
}

function toUtcMidnight(value: string): number {
  const [year, month, day] = value.split('-').map(Number);
  return Date.UTC(year ?? 1970, (month ?? 1) - 1, day ?? 1);
}

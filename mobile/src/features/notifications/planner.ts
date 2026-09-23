import type { InventoryItem } from '@/api/types';
import { addDays, fromDateString, toLocalDateString } from '@/utils/dates';

/**
 * Planification des rappels de péremption, en fonction pure (testable sans téléphone).
 *
 * Règles (voir CLAUDE.md, fonctionnalité 5) :
 * - un seul résumé par jour, à 18 h (le moment où l'on se demande quoi cuisiner) ;
 * - DLC : rappel la veille et le jour même ;
 * - DDM : un seul rappel le jour même, au ton « à vérifier » (jamais « périmé ») ;
 * - produits communs et produits perso de l'utilisateur ; pas ceux des autres membres ;
 * - horizon de 14 jours : au plus 14 notifications programmées, loin de la limite
 *   d'iOS (64 notifications programmées par app).
 */

export const REMINDER_HOUR = 18;
export const REMINDER_HORIZON_DAYS = 14;
// Au-delà, le texte devient illisible sur l'écran verrouillé : « et 3 autres ».
export const MAX_NAMES_IN_BODY = 4;
// Préfixe des identifiants : permet de retrouver (et d'annuler) nos rappels, et seulement eux.
export const REMINDER_ID_PREFIX = 'expiry-summary-';

export type ReminderItem = Pick<InventoryItem, 'name' | 'expiresOn' | 'expiryKind' | 'ownerUserId' | 'status'>;

export type PlannedReminder = {
  // « expiry-summary-2026-09-23 » : un rappel par jour au maximum.
  identifier: string;
  day: string;
  fireAt: Date;
  title: string;
  body: string;
};

type Reason = 'today' | 'tomorrow' | 'check';

// Ordre d'affichage : le plus urgent d'abord.
const REASON_ORDER: Record<Reason, number> = { today: 0, tomorrow: 1, check: 2 };

const REASON_LABELS: Record<Reason, string> = {
  today: 'aujourd\'hui',
  tomorrow: 'demain',
  check: 'à vérifier',
};

export function planExpiryReminders(items: readonly ReminderItem[], myUserId: string, now: Date): PlannedReminder[] {
  const relevant = items.filter(
    (i) => i.status === 'Active' && (i.ownerUserId === null || i.ownerUserId === myUserId),
  );
  const today = toLocalDateString(now);
  const reminders: PlannedReminder[] = [];

  for (let offset = 0; offset < REMINDER_HORIZON_DAYS; offset++) {
    const day = addDays(today, offset);
    const fireAt = at(day, REMINDER_HOUR);
    // Le rappel de 18 h d'aujourd'hui est déjà passé : on commence demain.
    if (fireAt.getTime() <= now.getTime()) continue;

    const entries = relevant
      .map((item) => ({ name: item.name, reason: reasonFor(item, day) }))
      .filter((e): e is { name: string; reason: Reason } => e.reason !== null)
      .sort((a, b) => REASON_ORDER[a.reason] - REASON_ORDER[b.reason] || a.name.localeCompare(b.name, 'fr'));

    if (entries.length === 0) continue;

    reminders.push({
      identifier: `${REMINDER_ID_PREFIX}${day}`,
      day,
      fireAt,
      title: reminderTitle(entries.map((e) => e.reason)),
      body: reminderBody(entries),
    });
  }

  return reminders;
}

/** Vrai si l'identifiant désigne un de nos rappels de péremption. */
export function isExpiryReminder(identifier: string): boolean {
  return identifier.startsWith(REMINDER_ID_PREFIX);
}

function reasonFor(item: ReminderItem, day: string): Reason | null {
  if (item.expiryKind === 'UseBy') {
    if (item.expiresOn === day) return 'today';
    if (item.expiresOn === addDays(day, 1)) return 'tomorrow';
    return null;
  }
  // DDM : encore consommable après la date, un seul rappel, sans alarmer.
  return item.expiresOn === day ? 'check' : null;
}

function reminderTitle(reasons: Reason[]): string {
  const count = reasons.length;
  const products = `${count} produit${count > 1 ? 's' : ''}`;
  return reasons.every((r) => r === 'check') ? `Ce soir : ${products} à vérifier` : `Ce soir : ${products} à utiliser`;
}

function reminderBody(entries: { name: string; reason: Reason }[]): string {
  const shown = entries.slice(0, MAX_NAMES_IN_BODY).map((e) => `${e.name} (${REASON_LABELS[e.reason]})`);
  const hidden = entries.length - shown.length;
  const list = hidden > 0 ? `${shown.join(', ')} et ${hidden} autre${hidden > 1 ? 's' : ''}` : shown.join(', ');
  return `${list}. Une idée de recette avec ? Demande-la dans l'onglet Recettes.`;
}

/**
 * Date « jour » + heure, en heure LOCALE. Construire la date à partir de ses composants
 * locaux gère les changements d'heure : le 25 octobre, 18 h reste 18 h.
 */
function at(day: string, hour: number): Date {
  const date = fromDateString(day);
  date.setHours(hour, 0, 0, 0);
  return date;
}

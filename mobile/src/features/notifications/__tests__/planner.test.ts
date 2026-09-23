// Types de Jest chargés ici seulement : TypeScript 6 ne les inclut plus automatiquement,
// et on évite ainsi de les exposer au code de l'app.
/// <reference types="jest" />

import { isExpiryReminder, planExpiryReminders, REMINDER_HORIZON_DAYS, type ReminderItem } from '../planner';

const ME = 'me';

function item(name: string, expiresOn: string, overrides: Partial<ReminderItem> = {}): ReminderItem {
  return { name, expiresOn, expiryKind: 'UseBy', ownerUserId: null, status: 'Active', ...overrides };
}

// Mercredi 23 septembre 2026, heure LOCALE du téléphone.
const MORNING = new Date(2026, 8, 23, 10, 0);
const EVENING = new Date(2026, 8, 23, 19, 0);

const days = (plan: { day: string }[]) => plan.map((r) => r.day);

describe('planExpiryReminders : quels jours', () => {
  it('DLC : un rappel la veille et un le jour même', () => {
    const plan = planExpiryReminders([item('Steak haché', '2026-09-26')], ME, MORNING);

    expect(days(plan)).toEqual(['2026-09-25', '2026-09-26']);
    expect(plan[0]?.body).toContain('Steak haché (demain)');
    expect(plan[1]?.body).toContain('Steak haché (aujourd\'hui)');
  });

  it('DDM : un seul rappel, le jour même, au ton « à vérifier » (jamais « périmé »)', () => {
    const plan = planExpiryReminders([item('Biscuits', '2026-09-26', { expiryKind: 'BestBefore' })], ME, MORNING);

    expect(days(plan)).toEqual(['2026-09-26']);
    expect(plan[0]?.title).toBe('Ce soir : 1 produit à vérifier');
    expect(plan[0]?.body).toContain('Biscuits (à vérifier)');
    expect(`${plan[0]?.title} ${plan[0]?.body}`.toLowerCase()).not.toContain('périm');
  });

  it('avant 18 h, le rappel du jour est encore programmé', () => {
    const plan = planExpiryReminders([item('Yaourts', '2026-09-23')], ME, MORNING);
    expect(days(plan)).toEqual(['2026-09-23']);
  });

  it('après 18 h, le rappel du jour est passé : on ne le programme plus', () => {
    const plan = planExpiryReminders([item('Yaourts', '2026-09-23'), item('Courgette', '2026-09-24')], ME, EVENING);

    // Seul reste le rappel de demain, où la courgette est « aujourd'hui ».
    expect(days(plan)).toEqual(['2026-09-24']);
    expect(plan[0]?.body).not.toContain('Yaourts');
  });

  it('ignore les produits déjà dépassés', () => {
    expect(planExpiryReminders([item('Jambon', '2026-09-20')], ME, MORNING)).toEqual([]);
  });

  it('horizon de 14 jours : rien au-delà', () => {
    const lastDay = '2026-10-06'; // aujourd'hui + 13
    const plan = planExpiryReminders(
      [item('A', lastDay), item('B', '2026-10-07'), item('C', '2026-10-08')],
      ME,
      MORNING,
    );

    // A : la veille et le jour même ; B : sa veille est le dernier jour de l'horizon ; C : trop loin.
    expect(days(plan)).toEqual(['2026-10-05', lastDay]);
    expect(plan[1]?.body).toContain('A (aujourd\'hui), B (demain)');
    expect(plan.some((r) => r.body.includes('C ('))).toBe(false);
  });

  it(`jamais plus de ${REMINDER_HORIZON_DAYS} rappels, même avec un frigo plein`, () => {
    const many = Array.from({ length: 60 }, (_, i) => item(`Produit ${i}`, `2026-09-${String(23 + (i % 7)).padStart(2, '0')}`));
    const plan = planExpiryReminders(many, ME, MORNING);

    expect(plan.length).toBeLessThanOrEqual(REMINDER_HORIZON_DAYS);
    // Un seul rappel par jour.
    expect(new Set(days(plan)).size).toBe(plan.length);
  });
});

describe('planExpiryReminders : quels produits', () => {
  it('produits communs et produits perso de l\'utilisateur, pas ceux des autres membres', () => {
    const plan = planExpiryReminders(
      [
        item('Lait (commun)', '2026-09-25'),
        item('Skyr (à moi)', '2026-09-25', { ownerUserId: ME }),
        item('Tofu (à Bob)', '2026-09-25', { ownerUserId: 'bob' }),
      ],
      ME,
      MORNING,
    );

    const body = plan.map((r) => r.body).join(' ');
    expect(body).toContain('Lait (commun)');
    expect(body).toContain('Skyr (à moi)');
    expect(body).not.toContain('Tofu');
  });

  it('ignore les produits consommés ou jetés', () => {
    const plan = planExpiryReminders(
      [item('Pain', '2026-09-25', { status: 'Consumed' }), item('Crème', '2026-09-25', { status: 'Discarded' })],
      ME,
      MORNING,
    );
    expect(plan).toEqual([]);
  });

  it('rien à signaler : aucun rappel', () => {
    expect(planExpiryReminders([], ME, MORNING)).toEqual([]);
  });
});

describe('planExpiryReminders : le texte', () => {
  it('un seul résumé par jour, le plus urgent d\'abord, avec une suggestion de recette', () => {
    const plan = planExpiryReminders(
      [
        item('Biscuits', '2026-09-25', { expiryKind: 'BestBefore' }),
        item('Yaourts', '2026-09-26'),
        item('Steak haché', '2026-09-25'),
      ],
      ME,
      MORNING,
    );
    const reminder = plan.find((r) => r.day === '2026-09-25');

    expect(reminder?.title).toBe('Ce soir : 3 produits à utiliser');
    expect(reminder?.body).toBe(
      'Steak haché (aujourd\'hui), Yaourts (demain), Biscuits (à vérifier). Une idée de recette avec ? Demande-la dans l\'onglet Recettes.',
    );
  });

  it('au-delà de 4 produits : « et N autres »', () => {
    const plan = planExpiryReminders(
      ['A', 'B', 'C', 'D', 'E', 'F'].map((name) => item(name, '2026-09-25')),
      ME,
      MORNING,
    );
    const reminder = plan.find((r) => r.day === '2026-09-25');

    expect(reminder?.title).toBe('Ce soir : 6 produits à utiliser');
    expect(reminder?.body).toMatch(/^A \(aujourd'hui\), B \(aujourd'hui\), C \(aujourd'hui\), D \(aujourd'hui\) et 2 autres\. /);
  });
});

describe('planExpiryReminders : l\'heure', () => {
  it('programme à 18 h, heure locale, avec un identifiant par jour', () => {
    const [reminder] = planExpiryReminders([item('Yaourts', '2026-09-23')], ME, MORNING);

    expect(reminder?.identifier).toBe('expiry-summary-2026-09-23');
    expect(reminder?.fireAt).toEqual(new Date(2026, 8, 23, 18, 0));
  });

  it('reste à 18 h le jour du passage à l\'heure d\'hiver (25 octobre 2026)', () => {
    const now = new Date(2026, 9, 24, 10, 0);
    const plan = planExpiryReminders([item('Yaourts', '2026-10-25')], ME, now);
    const sunday = plan.find((r) => r.day === '2026-10-25');

    expect([sunday?.fireAt.getDate(), sunday?.fireAt.getHours(), sunday?.fireAt.getMinutes()]).toEqual([25, 18, 0]);
  });
});

describe('isExpiryReminder', () => {
  it('reconnaît nos rappels à leur préfixe', () => {
    expect(isExpiryReminder('expiry-summary-2026-09-23')).toBe(true);
    expect(isExpiryReminder('autre-notification')).toBe(false);
  });
});

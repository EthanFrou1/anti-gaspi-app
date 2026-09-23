// Types de Jest chargés ici seulement : TypeScript 6 ne les inclut plus automatiquement,
// et on évite ainsi de les exposer au code de l'app.
/// <reference types="jest" />

import type { InventoryItem } from '@/api/types';
import { addDays, toLocalDateString } from '@/utils/dates';

/**
 * Tests de la synchronisation des rappels. Le module natif `expo-notifications` est
 * remplacé par un faux qui garde en mémoire les notifications programmées et affichées,
 * et l'API par une fausse liste de produits.
 *
 * Le module garde un état interne (configuration faite, file d'attente) : chaque test
 * le recharge à neuf.
 */

type FakeRequest = {
  identifier: string;
  content: { title: string; body: string };
  trigger: { type: string; date?: Date; seconds?: number; channelId: string };
};

jest.mock('expo-notifications', () => {
  const state = {
    permission: { granted: true, status: 'granted' },
    scheduled: [] as FakeRequest[],
    presented: [] as { request: { identifier: string } }[],
  };
  return {
    __state: state,
    PermissionStatus: { UNDETERMINED: 'undetermined', GRANTED: 'granted', DENIED: 'denied' },
    AndroidImportance: { DEFAULT: 3 },
    SchedulableTriggerInputTypes: { DATE: 'date', TIME_INTERVAL: 'timeInterval' },
    setNotificationHandler: jest.fn(),
    setNotificationChannelAsync: jest.fn(async () => null),
    getPermissionsAsync: jest.fn(async () => state.permission),
    requestPermissionsAsync: jest.fn(async () => {
      state.permission = { granted: true, status: 'granted' };
      return state.permission;
    }),
    getAllScheduledNotificationsAsync: jest.fn(async () => [...state.scheduled]),
    // Petite pause : laisse à une autre synchronisation l'occasion de s'intercaler.
    cancelScheduledNotificationAsync: jest.fn(async (id: string) => {
      await new Promise((resolve) => setTimeout(resolve, 1));
      state.scheduled = state.scheduled.filter((n) => n.identifier !== id);
    }),
    scheduleNotificationAsync: jest.fn(async (request: FakeRequest) => {
      state.scheduled.push(request);
      return request.identifier;
    }),
    getPresentedNotificationsAsync: jest.fn(async () => [...state.presented]),
    dismissNotificationAsync: jest.fn(async (id: string) => {
      state.presented = state.presented.filter((n) => n.request.identifier !== id);
    }),
  };
});

jest.mock('@/api/client', () => ({ api: { inventory: { list: jest.fn() } } }));

type FakeNotifications = {
  __state: {
    permission: { granted: boolean; status: string };
    scheduled: FakeRequest[];
    presented: { request: { identifier: string } }[];
  };
  requestPermissionsAsync: jest.Mock;
};

let notifications: FakeNotifications;
let listItems: jest.Mock;
let reminders: typeof import('../reminders');

// Dans 3 jours : les rappels (veille et jour même) sont toujours dans le futur,
// quelle que soit l'heure à laquelle les tests tournent.
const inThreeDays = addDays(toLocalDateString(), 3);

function item(name: string, expiresOn = inThreeDays): InventoryItem {
  return {
    id: name,
    name,
    categoryId: 1,
    quantity: 1,
    unit: 'Piece',
    purchasedOn: toLocalDateString(),
    expiresOn,
    expiryIsEstimated: true,
    expiryKind: 'UseBy',
    barcode: null,
    ownerUserId: null,
    ownerDisplayName: null,
    status: 'Active',
    statusChangedAt: null,
    createdAt: '2026-09-23T10:00:00Z',
  };
}

const scheduledIds = () => notifications.__state.scheduled.map((n) => n.identifier).sort();
const ours = (day: string) => `expiry-summary-${day}`;

beforeEach(() => {
  jest.resetModules();
  notifications = require('expo-notifications');
  listItems = require('@/api/client').api.inventory.list;
  reminders = require('../reminders');
});

describe('syncExpiryReminders', () => {
  it('programme le plan à 18 h (déclencheur à date fixe, canal Android dédié)', async () => {
    await reminders.syncExpiryReminders([item('Steak haché')], 'me');

    expect(scheduledIds()).toEqual([ours(addDays(inThreeDays, -1)), ours(inThreeDays)]);
    const [first] = notifications.__state.scheduled;
    expect(first?.trigger).toMatchObject({ type: 'date', channelId: 'expiry-reminders' });
    expect(first?.trigger.date?.getHours()).toBe(18);
  });

  it('remplace les rappels précédents et ne touche pas aux autres notifications', async () => {
    notifications.__state.scheduled.push({ identifier: 'autre-notification' } as FakeRequest);
    await reminders.syncExpiryReminders([item('Steak haché')], 'me');

    // Le steak a été consommé : plus rien à rappeler.
    await reminders.syncExpiryReminders([], 'me');

    expect(scheduledIds()).toEqual(['autre-notification']);
  });

  it('deux synchronisations simultanées ne laissent pas de doublons (file d\'attente)', async () => {
    await Promise.all([
      reminders.syncExpiryReminders([item('Steak haché')], 'me'),
      reminders.syncExpiryReminders([item('Steak haché')], 'me'),
    ]);

    expect(scheduledIds()).toEqual([ours(addDays(inThreeDays, -1)), ours(inThreeDays)]);
  });

  it('sans autorisation : annule les anciens rappels mais n\'en programme pas', async () => {
    await reminders.syncExpiryReminders([item('Steak haché')], 'me');
    notifications.__state.permission = { granted: false, status: 'denied' };

    await reminders.syncExpiryReminders([item('Yaourts')], 'me');

    expect(scheduledIds()).toEqual([]);
  });
});

describe('refreshExpiryReminders', () => {
  it('recharge l\'inventaire du foyer puis reprogramme', async () => {
    listItems.mockResolvedValue([item('Yaourts')]);

    await reminders.refreshExpiryReminders('h1', 'me');

    expect(listItems).toHaveBeenCalledWith('h1');
    expect(scheduledIds()).toHaveLength(2);
  });

  it('sans foyer : plus aucun rappel', async () => {
    await reminders.syncExpiryReminders([item('Yaourts')], 'me');

    await reminders.refreshExpiryReminders(null, 'me');

    expect(scheduledIds()).toEqual([]);
    expect(listItems).not.toHaveBeenCalled();
  });

  it('une erreur réseau est absorbée et ne bloque pas les synchronisations suivantes', async () => {
    const warn = jest.spyOn(console, 'warn').mockImplementation(() => {});
    listItems.mockRejectedValue(new Error('réseau'));

    await expect(reminders.refreshExpiryReminders('h1', 'me')).resolves.toBeUndefined();
    await reminders.syncExpiryReminders([item('Yaourts')], 'me');

    expect(scheduledIds()).toHaveLength(2);
    warn.mockRestore();
  });
});

describe('clearExpiryReminders', () => {
  it('annule les rappels programmés et retire ceux déjà affichés (noms de produits)', async () => {
    await reminders.syncExpiryReminders([item('Steak haché')], 'me');
    notifications.__state.presented = [
      { request: { identifier: ours('2026-09-22') } },
      { request: { identifier: 'autre-notification' } },
    ];

    await reminders.clearExpiryReminders();

    expect(scheduledIds()).toEqual([]);
    expect(notifications.__state.presented.map((n) => n.request.identifier)).toEqual(['autre-notification']);
  });
});

describe('askReminderPermission', () => {
  it('première fois : demande l\'autorisation, puis programme les rappels', async () => {
    notifications.__state.permission = { granted: false, status: 'undetermined' };
    listItems.mockResolvedValue([item('Yaourts')]);

    await reminders.askReminderPermission('h1', 'me');

    expect(notifications.requestPermissionsAsync).toHaveBeenCalledTimes(1);
    expect(scheduledIds()).toHaveLength(2);
  });

  it.each(['denied', 'granted'])('déjà répondu (%s) : on ne redemande pas', async (status) => {
    notifications.__state.permission = { granted: status === 'granted', status };

    await reminders.askReminderPermission('h1', 'me');

    expect(notifications.requestPermissionsAsync).not.toHaveBeenCalled();
  });
});

describe('sendTestReminder (outil de développement)', () => {
  it('envoie dans 10 secondes le vrai prochain résumé, reconnu comme un de nos rappels', async () => {
    const result = await reminders.sendTestReminder([item('Steak haché')], 'me');

    expect(result).toBe('sent');
    const [sent] = notifications.__state.scheduled;
    expect(sent?.identifier).toBe('expiry-summary-dev-test');
    expect(sent?.trigger).toMatchObject({ type: 'timeInterval', seconds: 10, channelId: 'expiry-reminders' });
    expect(sent?.content.body).toContain('Steak haché (demain)');
  });

  it('rien ne périme bientôt : rien n\'est envoyé', async () => {
    const result = await reminders.sendTestReminder([item('Pâtes', addDays(inThreeDays, 60))], 'me');

    expect(result).toBe('nothing-planned');
    expect(scheduledIds()).toEqual([]);
  });

  it('sans autorisation : ne la demande pas (le vrai parcours reste à tester)', async () => {
    notifications.__state.permission = { granted: false, status: 'undetermined' };

    const result = await reminders.sendTestReminder([item('Steak haché')], 'me');

    expect(result).toBe('not-allowed');
    expect(notifications.requestPermissionsAsync).not.toHaveBeenCalled();
    expect(scheduledIds()).toEqual([]);
  });
});

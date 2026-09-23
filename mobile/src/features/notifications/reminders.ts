import * as Notifications from 'expo-notifications';
import { Platform } from 'react-native';
import { api } from '@/api/client';
import type { InventoryItem } from '@/api/types';
import { isExpiryReminder, planExpiryReminders, REMINDER_ID_PREFIX } from './planner';

/**
 * Rappels de péremption en notifications LOCALES : c'est le téléphone qui les programme,
 * à partir de l'inventaire. Aucun serveur, aucun jeton d'appareil, et ça marche dans Expo Go.
 *
 * Principe : à chaque synchronisation, on annule tous nos rappels puis on reprogramme
 * ceux du plan (fonction pure, voir planner.ts). L'opération est idempotente : la
 * relancer ne crée jamais de doublon.
 *
 * Limite assumée : un produit ajouté par un colocataire n'est pris en compte qu'à la
 * prochaine ouverture de l'app (le push, prévu en V2, lèvera cette limite).
 */

// Android 8+ : chaque notification appartient à un « canal », réglable par l'utilisateur.
const CHANNEL_ID = 'expiry-reminders';

// Rappel de test (outil de développement), voir sendTestReminder.
const TEST_REMINDER_ID = `${REMINDER_ID_PREFIX}dev-test`;

// Web et tests d'autres plateformes : pas de notifications programmées.
const supported = Platform.OS === 'ios' || Platform.OS === 'android';

let setupPromise: Promise<void> | null = null;

/**
 * Configuration unique, à lancer au démarrage. Sur Android 13+, le canal doit exister
 * AVANT la demande de permission, sinon le système n'affiche pas la demande.
 */
export function setupNotifications(): Promise<void> {
  if (!supported) return Promise.resolve();
  setupPromise ??= (async () => {
    // Notification reçue app ouverte : on l'affiche quand même (bandeau et centre de notifications).
    Notifications.setNotificationHandler({
      handleNotification: async () => ({
        shouldShowBanner: true,
        shouldShowList: true,
        // Sur Android, sans son, le bandeau n'apparaît pas au premier plan.
        shouldPlaySound: true,
        shouldSetBadge: false,
      }),
    });
    if (Platform.OS === 'android') {
      await Notifications.setNotificationChannelAsync(CHANNEL_ID, {
        name: 'Produits à consommer',
        description: 'Résumé quotidien des produits qui arrivent à leur date.',
        importance: Notifications.AndroidImportance.DEFAULT,
      });
    }
  })().catch((error: unknown) => {
    // On pourra réessayer au prochain appel.
    setupPromise = null;
    throw error;
  });
  return setupPromise;
}

/**
 * File d'attente : les synchronisations s'exécutent l'une après l'autre. Sans elle, deux
 * synchronisations simultanées (retour sur le Frigo et retour de l'app au premier plan)
 * pourraient entrelacer leurs « annuler » et « programmer » et laisser des doublons.
 */
let queue: Promise<void> = Promise.resolve();

function enqueue(task: () => Promise<void>): Promise<void> {
  const run = queue.then(task).catch((error: unknown) => {
    // Les rappels sont un « bonus » : un échec ne doit jamais gêner l'utilisateur.
    if (__DEV__) console.warn('Rappels de péremption : synchronisation impossible', error);
  });
  queue = run;
  return run;
}

/** Reprogramme les rappels à partir d'un inventaire déjà chargé (écran Frigo). */
export function syncExpiryReminders(items: readonly InventoryItem[], myUserId: string): Promise<void> {
  if (!supported) return Promise.resolve();
  return enqueue(() => applyPlan(items, myUserId));
}

/**
 * Recharge l'inventaire puis reprogramme (retour au premier plan, « J'ai cuisiné »…).
 * Le chargement se fait DANS la file : on travaille toujours sur les données les plus récentes.
 */
export function refreshExpiryReminders(householdId: string | null, myUserId: string): Promise<void> {
  if (!supported) return Promise.resolve();
  return enqueue(async () => {
    if (!householdId) {
      await cancelScheduled();
      return;
    }
    const items = await api.inventory.list(householdId);
    await applyPlan(items, myUserId);
  });
}

/**
 * Déconnexion, suppression du compte, session expirée : on annule les rappels à venir et
 * on retire ceux déjà affichés (ils contiennent des noms de produits).
 */
export function clearExpiryReminders(): Promise<void> {
  if (!supported) return Promise.resolve();
  return enqueue(async () => {
    await cancelScheduled();
    const presented = await Notifications.getPresentedNotificationsAsync();
    await Promise.all(
      presented
        .filter((n) => isExpiryReminder(n.request.identifier))
        .map((n) => Notifications.dismissNotificationAsync(n.request.identifier)),
    );
  });
}

/**
 * Demande l'autorisation après l'ajout d'un produit : l'utilisateur comprend alors à quoi
 * elle sert (demandée au démarrage, elle est souvent refusée par réflexe).
 * On ne demande qu'une fois : si l'utilisateur a déjà répondu, on ne le relance pas
 * (il peut toujours changer d'avis dans les réglages du téléphone).
 */
export async function askReminderPermission(householdId: string, myUserId: string): Promise<void> {
  if (!supported) return;
  try {
    await setupNotifications();
    const current = await Notifications.getPermissionsAsync();
    if (current.status !== Notifications.PermissionStatus.UNDETERMINED) return;

    const result = await Notifications.requestPermissionsAsync({
      ios: { allowAlert: true, allowSound: true, allowBadge: false },
    });
    if (result.granted) {
      await refreshExpiryReminders(householdId, myUserId);
    }
  } catch (error) {
    if (__DEV__) console.warn('Rappels de péremption : demande d\'autorisation impossible', error);
  }
}

export type TestReminderResult = 'sent' | 'nothing-planned' | 'not-allowed';

/**
 * Outil de mise au point (build de développement uniquement, comme le bouton 🤖) :
 * envoie dans 10 secondes le VRAI prochain résumé, pour ne pas attendre 18 h.
 * Il porte le préfixe de nos rappels : l'appui ouvre le Frigo, la déconnexion l'efface.
 * Une synchronisation dans les 10 secondes (retour de l'app au premier plan) l'annule.
 *
 * Ne demande pas l'autorisation : on garde intact le parcours réel (demande après
 * l'ajout d'un produit), qui fait partie des tests sur appareil.
 */
export async function sendTestReminder(items: readonly InventoryItem[], myUserId: string): Promise<TestReminderResult> {
  // Double sécurité : même appelée par erreur dans l'app publiée, la fonction ne fait rien.
  if (!__DEV__ || !supported) return 'nothing-planned';

  const [next] = planExpiryReminders(items, myUserId, new Date());
  if (!next) return 'nothing-planned';

  await setupNotifications();
  const { granted } = await Notifications.getPermissionsAsync();
  if (!granted) return 'not-allowed';

  await Notifications.scheduleNotificationAsync({
    identifier: TEST_REMINDER_ID,
    content: { title: next.title, body: next.body, data: { kind: 'expiry-summary' } },
    trigger: {
      type: Notifications.SchedulableTriggerInputTypes.TIME_INTERVAL,
      seconds: 10,
      channelId: CHANNEL_ID,
    },
  });
  return 'sent';
}

async function applyPlan(items: readonly InventoryItem[], myUserId: string): Promise<void> {
  await setupNotifications();
  await cancelScheduled();

  const { granted } = await Notifications.getPermissionsAsync();
  if (!granted) return;

  for (const reminder of planExpiryReminders(items, myUserId, new Date())) {
    await Notifications.scheduleNotificationAsync({
      identifier: reminder.identifier,
      content: {
        title: reminder.title,
        body: reminder.body,
        // Lu à l'appui sur la notification (voir useReminderTaps).
        data: { kind: 'expiry-summary' },
      },
      trigger: {
        type: Notifications.SchedulableTriggerInputTypes.DATE,
        date: reminder.fireAt,
        channelId: CHANNEL_ID,
      },
    });
  }
}

/** Annule nos rappels programmés, et seulement eux (d'autres notifications pourront exister). */
async function cancelScheduled(): Promise<void> {
  const scheduled = await Notifications.getAllScheduledNotificationsAsync();
  await Promise.all(
    scheduled
      .filter((n) => isExpiryReminder(n.identifier))
      .map((n) => Notifications.cancelScheduledNotificationAsync(n.identifier)),
  );
}

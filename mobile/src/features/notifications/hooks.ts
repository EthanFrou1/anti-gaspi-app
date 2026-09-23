import * as Notifications from 'expo-notifications';
import { router } from 'expo-router';
import { useEffect } from 'react';
import { AppState } from 'react-native';
import type { AuthState } from '@/auth/AuthContext';
import { isExpiryReminder } from './planner';
import { clearExpiryReminders, refreshExpiryReminders, setupNotifications } from './reminders';

/**
 * Cycle de vie des rappels, piloté par l'état de session (monté à la racine de l'app) :
 * - connecté : reprogramme à la connexion, au changement de foyer et à chaque retour
 *   de l'app au premier plan (un colocataire a pu changer l'inventaire entre-temps) ;
 * - déconnecté (déconnexion, compte supprimé, session expirée) : efface tout ;
 * - serveur injoignable : on garde les rappels déjà programmés.
 */
export function useExpiryReminderSync(state: AuthState) {
  const status = state.status;
  const userId = state.status === 'signedIn' ? state.user.id : null;
  const householdId = state.status === 'signedIn' ? state.user.householdId : null;

  useEffect(() => {
    setupNotifications().catch((error: unknown) => {
      if (__DEV__) console.warn('Notifications : configuration impossible', error);
    });
  }, []);

  useEffect(() => {
    if (status === 'signedOut') {
      void clearExpiryReminders();
    } else if (status === 'signedIn' && userId) {
      void refreshExpiryReminders(householdId, userId);
    }
  }, [status, userId, householdId]);

  useEffect(() => {
    if (!userId) return;
    const subscription = AppState.addEventListener('change', (next) => {
      if (next === 'active') void refreshExpiryReminders(householdId, userId);
    });
    return () => subscription.remove();
  }, [userId, householdId]);
}

/**
 * Appui sur un rappel : ouvre l'onglet Frigo. `useLastNotificationResponse` couvre aussi
 * le cas où l'app était fermée et a été lancée par l'appui.
 *
 * Sécurité : on ne suit jamais une adresse lue dans la notification ; on reconnaît nos
 * rappels à leur identifiant et on navigue vers une route fixe.
 */
export function useReminderTaps(enabled: boolean) {
  const response = Notifications.useLastNotificationResponse();

  useEffect(() => {
    if (!enabled || !response) return;
    if (response.actionIdentifier !== Notifications.DEFAULT_ACTION_IDENTIFIER) return;
    if (!isExpiryReminder(response.notification.request.identifier)) return;

    // Consommé : on ne redirige pas une deuxième fois au prochain rendu.
    Notifications.clearLastNotificationResponse();
    router.navigate('/');
  }, [enabled, response]);
}

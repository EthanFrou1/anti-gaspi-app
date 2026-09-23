import type { Household, HouseholdMember, Invitation } from '@/api/types';

/**
 * Règles d'affichage du foyer, en fonctions pures (testables sans interface).
 * Le serveur reste la référence : ces fonctions ne servent qu'à informer
 * l'utilisateur avant qu'il agisse.
 */

/** « ABCD2345 » → « ABCD-2345 », plus facile à lire et à dicter. */
export function formatInvitationCode(code: string): string {
  return code.length === 8 ? `${code.slice(0, 4)}-${code.slice(4)}` : code;
}

/** Un membre révoque ses propres invitations ; le propriétaire peut toutes les révoquer. */
export function canRevokeInvitation(invitation: Invitation, household: Household, myUserId: string): boolean {
  return household.myRole === 'Owner' || invitation.createdByUserId === myUserId;
}

/** Seul le propriétaire exclut, et jamais lui-même (pour lui, c'est « quitter »). */
export function canRemoveMember(member: HouseholdMember, household: Household, myUserId: string): boolean {
  return household.myRole === 'Owner' && member.userId !== myUserId;
}

/**
 * Membre qui deviendrait propriétaire si l'utilisateur partait :
 * le plus ancien (date d'arrivée) parmi les autres membres.
 */
export function nextOwner(household: Household, myUserId: string): HouseholdMember | undefined {
  return household.members
    .filter((m) => m.userId !== myUserId)
    .sort((a, b) => a.joinedAt.localeCompare(b.joinedAt))[0];
}

/** Explique ce qui se passera si l'utilisateur quitte le foyer (ou supprime son compte). */
export function describeLeaveConsequence(household: Household, myUserId: string): string {
  const successor = nextOwner(household, myUserId);
  if (!successor) {
    return 'Tu es le seul membre : le foyer et tout son contenu seront supprimés.';
  }
  if (household.myRole === 'Owner') {
    return `${successor.displayName} deviendra propriétaire du foyer (membre le plus ancien).`;
  }
  return 'Tu pourras ensuite créer un foyer ou en rejoindre un autre.';
}

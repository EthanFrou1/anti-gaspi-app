// Types de Jest chargés ici seulement : TypeScript 6 ne les inclut plus automatiquement,
// et on évite ainsi de les exposer au code de l'app.
/// <reference types="jest" />

import type { Household, HouseholdMember, Invitation } from '@/api/types';
import {
  canRemoveMember,
  canRevokeInvitation,
  describeLeaveConsequence,
  formatInvitationCode,
  nextOwner,
} from '../rules';

const owner: HouseholdMember = { userId: 'owner', displayName: 'Alice', role: 'Owner', joinedAt: '2026-01-01T10:00:00Z' };
const bob: HouseholdMember = { userId: 'bob', displayName: 'Bob', role: 'Member', joinedAt: '2026-01-02T10:00:00Z' };
const carol: HouseholdMember = { userId: 'carol', displayName: 'Carol', role: 'Member', joinedAt: '2026-01-03T10:00:00Z' };

function household(myRole: Household['myRole'], members: HouseholdMember[]): Household {
  return { id: 'h1', name: 'Coloc', createdAt: '2026-01-01T10:00:00Z', myRole, members };
}

function invitation(createdByUserId: string | null): Invitation {
  return { id: 'i1', code: 'ABCD2345', expiresAt: '2026-01-08T10:00:00Z', createdByUserId };
}

describe('formatInvitationCode', () => {
  it('coupe un code de 8 caractères en deux groupes', () => {
    expect(formatInvitationCode('ABCD2345')).toBe('ABCD-2345');
  });

  it('laisse tel quel un code inattendu', () => {
    expect(formatInvitationCode('ABC')).toBe('ABC');
  });
});

describe('canRevokeInvitation', () => {
  it('autorise un membre à révoquer sa propre invitation', () => {
    expect(canRevokeInvitation(invitation('bob'), household('Member', [owner, bob]), 'bob')).toBe(true);
  });

  it('interdit à un membre de révoquer celle d\'un autre', () => {
    expect(canRevokeInvitation(invitation('owner'), household('Member', [owner, bob]), 'bob')).toBe(false);
  });

  it('autorise le propriétaire à tout révoquer, même une invitation sans auteur', () => {
    expect(canRevokeInvitation(invitation(null), household('Owner', [owner, bob]), 'owner')).toBe(true);
  });
});

describe('canRemoveMember', () => {
  it('autorise le propriétaire à exclure un membre', () => {
    expect(canRemoveMember(bob, household('Owner', [owner, bob]), 'owner')).toBe(true);
  });

  it('n\'affiche pas « exclure » sur soi-même', () => {
    expect(canRemoveMember(owner, household('Owner', [owner, bob]), 'owner')).toBe(false);
  });

  it('interdit à un simple membre d\'exclure', () => {
    expect(canRemoveMember(carol, household('Member', [owner, bob, carol]), 'bob')).toBe(false);
  });
});

describe('nextOwner / describeLeaveConsequence', () => {
  it('désigne le membre arrivé le plus tôt, quel que soit l\'ordre de la liste', () => {
    expect(nextOwner(household('Owner', [owner, carol, bob]), 'owner')).toBe(bob);
  });

  it('prévient le propriétaire du transfert de propriété', () => {
    expect(describeLeaveConsequence(household('Owner', [owner, bob, carol]), 'owner')).toContain('Bob deviendra propriétaire');
  });

  it('prévient le dernier membre que le foyer sera supprimé', () => {
    expect(describeLeaveConsequence(household('Owner', [owner]), 'owner')).toContain('seront supprimés');
  });

  it('rassure un simple membre', () => {
    expect(describeLeaveConsequence(household('Member', [owner, bob]), 'bob')).toContain('rejoindre un autre');
  });
});

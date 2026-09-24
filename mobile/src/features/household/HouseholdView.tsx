import { useState } from 'react';
import { Alert, Text, View } from 'react-native';
import { api } from '@/api/client';
import { asApiError, type ApiError } from '@/api/errors';
import type { Household, HouseholdMember } from '@/api/types';
import { Avatar } from '@/components/Avatar';
import { Button } from '@/components/Button';
import { Card } from '@/components/Card';
import { ErrorBanner } from '@/components/ErrorBanner';
import { Crown } from '@/components/icons/lucide';
import { makeStyles, useTheme } from '@/theme';
import { EquipmentSection } from './EquipmentSection';
import { InvitationsSection } from './InvitationsSection';
import { canRemoveMember, describeLeaveConsequence } from './rules';

type Props = {
  household: Household;
  myUserId: string;
  // Appelé après un changement de composition du foyer (départ, exclusion).
  onChanged: () => Promise<void>;
};

export function HouseholdView({ household, myUserId, onChanged }: Props) {
  const theme = useTheme();
  const styles = useStyles();
  // Ordre d'arrivée : chaque membre garde la couleur d'avatar de ses produits perso.
  const memberIds = household.members.map((m) => m.userId);
  const [error, setError] = useState<ApiError | null>(null);
  const [leaving, setLeaving] = useState(false);

  async function removeMember(userId: string) {
    setError(null);
    try {
      await api.households.removeMember(household.id, userId);
      await onChanged();
    } catch (e) {
      setError(asApiError(e));
    }
  }

  function confirmExclude(member: HouseholdMember) {
    Alert.alert(
      `Exclure ${member.displayName} ?`,
      'Il ou elle n\'aura plus accès au foyer. Les codes d\'invitation actifs seront révoqués.',
      [
        { text: 'Annuler', style: 'cancel' },
        { text: 'Exclure', style: 'destructive', onPress: () => void removeMember(member.userId) },
      ],
    );
  }

  function confirmLeave() {
    Alert.alert('Quitter le foyer ?', describeLeaveConsequence(household, myUserId), [
      { text: 'Annuler', style: 'cancel' },
      {
        text: 'Quitter',
        style: 'destructive',
        onPress: () => {
          setLeaving(true);
          void removeMember(myUserId).finally(() => setLeaving(false));
        },
      },
    ]);
  }

  return (
    <View style={styles.container}>
      <Text style={styles.name}>{household.name}</Text>
      <ErrorBanner message={error?.message} />

      <View style={styles.section}>
        <Text style={styles.title}>Membres ({household.members.length})</Text>
        {household.members.map((member) => (
          <Card key={member.userId} style={styles.memberRow}>
            <Avatar userId={member.userId} displayName={member.displayName} size={36} memberIds={memberIds} />
            <View style={styles.memberText}>
              <Text style={styles.memberName}>
                {member.displayName}
                {member.userId === myUserId ? ' (toi)' : ''}
              </Text>
              {member.role === 'Owner' ? (
                <View style={styles.badge}>
                  <Crown size={14} strokeWidth={2} color={theme.persoBadge.fg} />
                  <Text style={styles.badgeText}>Propriétaire</Text>
                </View>
              ) : null}
            </View>
            {canRemoveMember(member, household, myUserId) ? (
              <Button
                title="Exclure"
                size="small"
                variant="danger"
                onPress={() => confirmExclude(member)}
                accessibilityLabel={`Exclure ${member.displayName}`}
              />
            ) : null}
          </Card>
        ))}
      </View>

      <EquipmentSection household={household} />

      <InvitationsSection household={household} myUserId={myUserId} />

      <Button title="Quitter le foyer" variant="secondary" onPress={confirmLeave} loading={leaving} />
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  container: { gap: t.space.xl },
  name: { ...t.type.title1, color: t.colors.ink },
  section: { gap: t.space.xs },
  title: { ...t.type.title3, color: t.colors.ink },
  memberRow: { flexDirection: 'row', alignItems: 'center', gap: t.space.sm, paddingVertical: t.space.sm },
  memberText: { flex: 1, gap: t.space.xxs },
  memberName: { ...t.type.bodyBold, color: t.colors.ink },
  // Badge neutre, comme « Perso » : une information, pas une alerte.
  badge: {
    flexDirection: 'row',
    alignItems: 'center',
    alignSelf: 'flex-start',
    gap: t.space.xxs,
    backgroundColor: t.persoBadge.bg,
    borderRadius: t.radius.pill,
    paddingHorizontal: t.space.xs,
    paddingVertical: 2,
  },
  badgeText: { ...t.type.caption, color: t.persoBadge.fg },
}));

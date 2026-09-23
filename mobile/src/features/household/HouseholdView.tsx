import { useState } from 'react';
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native';
import { api } from '@/api/client';
import { asApiError, type ApiError } from '@/api/errors';
import type { Household, HouseholdMember } from '@/api/types';
import { Button } from '@/components/Button';
import { ErrorBanner } from '@/components/ErrorBanner';
import { colors, spacing } from '@/theme';
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
          <View key={member.userId} style={styles.memberRow}>
            <Text style={styles.memberName}>
              {member.displayName}
              {member.userId === myUserId ? ' (toi)' : ''}
            </Text>
            {member.role === 'Owner' ? <Text style={styles.badge}>Propriétaire</Text> : null}
            {canRemoveMember(member, household, myUserId) ? (
              <Pressable onPress={() => confirmExclude(member)} accessibilityRole="button" hitSlop={8}>
                <Text style={styles.danger}>Exclure</Text>
              </Pressable>
            ) : null}
          </View>
        ))}
      </View>

      <EquipmentSection household={household} />

      <InvitationsSection household={household} myUserId={myUserId} />

      <Button title="Quitter le foyer" variant="secondary" onPress={confirmLeave} loading={leaving} />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { gap: spacing.lg },
  name: { fontSize: 26, fontWeight: '700', color: colors.text },
  section: { gap: spacing.sm },
  title: { fontSize: 18, fontWeight: '700', color: colors.text },
  memberRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    paddingVertical: spacing.sm,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: colors.border,
  },
  memberName: { flex: 1, fontSize: 16, color: colors.text },
  badge: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.primary,
    borderWidth: 1,
    borderColor: colors.primary,
    borderRadius: 4,
    paddingHorizontal: spacing.xs,
    paddingVertical: 2,
  },
  danger: { color: colors.error, fontWeight: '600' },
});

import { useCallback, useEffect, useState } from 'react';
import { Alert, Pressable, Share, StyleSheet, Text, View } from 'react-native';
import { api } from '@/api/client';
import { asApiError, type ApiError } from '@/api/errors';
import type { Household, Invitation } from '@/api/types';
import { Button } from '@/components/Button';
import { ErrorBanner } from '@/components/ErrorBanner';
import { APP_NAME } from '@/config';
import { colors, spacing } from '@/theme';
import { canRevokeInvitation, formatInvitationCode } from './rules';

type Props = {
  household: Household;
  myUserId: string;
};

export function InvitationsSection({ household, myUserId }: Props) {
  const [invitations, setInvitations] = useState<Invitation[]>([]);
  const [error, setError] = useState<ApiError | null>(null);
  const [creating, setCreating] = useState(false);

  const load = useCallback(async () => {
    try {
      setInvitations(await api.invitations.list(household.id));
    } catch (e) {
      setError(asApiError(e));
    }
  }, [household.id]);

  // Recharge aussi quand le foyer change (ex. après un « tirer pour rafraîchir »).
  useEffect(() => {
    void load();
  }, [load, household]);

  async function handleInvite() {
    setCreating(true);
    setError(null);
    try {
      const invitation = await api.invitations.create(household.id);
      await load();
      await share(invitation);
    } catch (e) {
      setError(asApiError(e));
    } finally {
      setCreating(false);
    }
  }

  function share(invitation: Invitation) {
    // Feuille de partage native (SMS, WhatsApp…) : aucune dépendance nécessaire.
    return Share.share({
      message:
        `Rejoins notre foyer « ${household.name} » sur ${APP_NAME} avec le code ` +
        `${formatInvitationCode(invitation.code)} (valable jusqu'au ${formatDate(invitation.expiresAt)}).`,
    });
  }

  function confirmRevoke(invitation: Invitation) {
    Alert.alert(
      'Révoquer ce code ?',
      `Le code ${formatInvitationCode(invitation.code)} ne permettra plus de rejoindre le foyer.`,
      [
        { text: 'Annuler', style: 'cancel' },
        { text: 'Révoquer', style: 'destructive', onPress: () => void revoke(invitation) },
      ],
    );
  }

  async function revoke(invitation: Invitation) {
    setError(null);
    try {
      await api.invitations.revoke(household.id, invitation.id);
      await load();
    } catch (e) {
      setError(asApiError(e));
    }
  }

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Inviter quelqu'un</Text>
      <ErrorBanner message={error?.message} />
      <Button title="Créer et partager un code" onPress={() => void handleInvite()} loading={creating} />

      {invitations.length > 0 ? <Text style={styles.subtitle}>Codes actifs</Text> : null}
      {invitations.map((invitation) => (
        <View key={invitation.id} style={styles.row}>
          <Pressable
            style={styles.codeBlock}
            onPress={() => void share(invitation)}
            accessibilityRole="button"
            accessibilityLabel={`Partager le code ${formatInvitationCode(invitation.code)}`}
          >
            <Text style={styles.code}>{formatInvitationCode(invitation.code)}</Text>
            <Text style={styles.meta}>Valable jusqu'au {formatDate(invitation.expiresAt)}</Text>
          </Pressable>
          {canRevokeInvitation(invitation, household, myUserId) ? (
            <Pressable onPress={() => confirmRevoke(invitation)} accessibilityRole="button" hitSlop={8}>
              <Text style={styles.revoke}>Révoquer</Text>
            </Pressable>
          ) : null}
        </View>
      ))}
    </View>
  );
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('fr-FR', { day: 'numeric', month: 'long' });
}

const styles = StyleSheet.create({
  container: { gap: spacing.md },
  title: { fontSize: 18, fontWeight: '700', color: colors.text },
  subtitle: { fontSize: 14, fontWeight: '600', color: colors.mutedText },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 8,
    padding: spacing.md,
  },
  codeBlock: { flex: 1, gap: spacing.xs },
  code: { fontSize: 20, fontWeight: '700', letterSpacing: 2, color: colors.text },
  meta: { fontSize: 13, color: colors.mutedText },
  revoke: { color: colors.error, fontWeight: '600' },
});

import { useCallback, useEffect, useState } from 'react';
import { Alert, Pressable, Share, Text, View } from 'react-native';
import { api } from '@/api/client';
import { asApiError, type ApiError } from '@/api/errors';
import type { Household, Invitation } from '@/api/types';
import { Button } from '@/components/Button';
import { ErrorBanner } from '@/components/ErrorBanner';
import { Share2 } from '@/components/icons/lucide';
import { APP_NAME } from '@/config';
import { makeStyles, useTheme } from '@/theme';
import { activeInvitationHint, canCreateInvitation, canRevokeInvitation, formatInvitationCode } from './rules';

type Props = {
  household: Household;
  myUserId: string;
};

export function InvitationsSection({ household, myUserId }: Props) {
  const theme = useTheme();
  const styles = useStyles();
  const [invitations, setInvitations] = useState<Invitation[]>([]);
  // Liste reçue au moins une fois : avant, on ne sait pas encore s'il existe un code actif.
  const [loaded, setLoaded] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);
  const [creating, setCreating] = useState(false);

  const load = useCallback(async () => {
    try {
      setInvitations(await api.invitations.list(household.id));
      setLoaded(true);
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
      // Un autre membre a pu créer le code entre-temps : on l'affiche.
      await load();
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
      {loaded && canCreateInvitation(invitations) ? (
        <Button title="Créer et partager un code" onPress={() => void handleInvite()} loading={creating} />
      ) : null}

      {invitations.length > 0 ? (
        <Text style={styles.subtitle}>{invitations.length === 1 ? 'Code actif' : 'Codes actifs'}</Text>
      ) : null}
      {invitations.map((invitation) => (
        <View key={invitation.id} style={styles.row}>
          <Pressable
            style={styles.codeBlock}
            onPress={() => void share(invitation)}
            accessibilityRole="button"
            accessibilityLabel={`Partager le code ${formatInvitationCode(invitation.code)}`}
          >
            <View style={styles.codeLine}>
              <Text style={styles.code}>{formatInvitationCode(invitation.code)}</Text>
              <Share2 size={18} strokeWidth={2} color={theme.colors.ink2} />
            </View>
            <Text style={styles.meta}>Valable jusqu'au {formatDate(invitation.expiresAt)} · toucher pour partager</Text>
          </Pressable>
          {canRevokeInvitation(invitation, household, myUserId) ? (
            <Button
              title="Révoquer"
              size="small"
              variant="danger"
              onPress={() => confirmRevoke(invitation)}
              accessibilityLabel={`Révoquer le code ${formatInvitationCode(invitation.code)}`}
            />
          ) : null}
        </View>
      ))}
      {invitations[0] ? (
        <Text style={styles.meta}>{activeInvitationHint(invitations[0], household, myUserId)}</Text>
      ) : null}
    </View>
  );
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('fr-FR', { day: 'numeric', month: 'long' });
}

const useStyles = makeStyles((t) => ({
  container: { gap: t.space.sm },
  title: { ...t.type.title3, color: t.colors.ink },
  subtitle: { ...t.type.overline, color: t.colors.ink3, marginTop: t.space.xs },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: t.space.sm,
    backgroundColor: t.colors.surface,
    borderWidth: t.borderWidth.hairline,
    borderColor: t.colors.line,
    borderRadius: t.radius.lg,
    padding: t.space.md,
  },
  codeBlock: { flex: 1, gap: t.space.xxs },
  codeLine: { flexDirection: 'row', alignItems: 'center', gap: t.space.xs },
  code: { ...t.type.title2, letterSpacing: 2, color: t.colors.ink },
  meta: { ...t.type.caption, color: t.colors.ink3 },
}));

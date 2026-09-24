import { Text, View } from 'react-native';
import { makeStyles, useTheme } from '@/theme';
import { Avatar } from './Avatar';

type Props = {
  ownerUserId: string;
  ownerDisplayName: string | null;
  memberIds?: readonly string[];
};

/**
 * Badge « Perso · Prénom » d'un produit perso : neutre, avec le mini-avatar du membre.
 * Jamais en framboise, couleur réservée à « Périmé » (règle de la charte).
 */
export function PersoBadge({ ownerUserId, ownerDisplayName, memberIds }: Props) {
  const theme = useTheme();
  const styles = useStyles();
  const name = ownerDisplayName ?? 'un membre';

  return (
    <View style={[styles.badge, { backgroundColor: theme.persoBadge.bg }]} accessibilityLabel={`Produit perso de ${name}`}>
      <Avatar userId={ownerUserId} displayName={ownerDisplayName} memberIds={memberIds} />
      <Text style={[styles.label, { color: theme.persoBadge.fg }]} numberOfLines={1}>
        Perso · {name}
      </Text>
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  badge: {
    flexDirection: 'row',
    alignItems: 'center',
    alignSelf: 'flex-start',
    gap: t.space.xxs + 2,
    borderRadius: t.radius.pill,
    paddingLeft: 2,
    paddingRight: t.space.xs,
    paddingVertical: 2,
  },
  label: { ...t.type.caption, flexShrink: 1 },
}));

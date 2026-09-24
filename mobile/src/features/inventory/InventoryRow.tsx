import { Text, View } from 'react-native';
import type { Category, InventoryItem } from '@/api/types';
import { Card } from '@/components/Card';
import { CategoryIcon } from '@/components/CategoryIcon';
import { IconButton } from '@/components/IconButton';
import { Trash, UtensilsCrossed } from '@/components/icons/lucide';
import { PersoBadge } from '@/components/PersoBadge';
import { UrgencyBadge } from '@/components/UrgencyBadge';
import { brandUrgency, makeStyles, useTheme } from '@/theme';
import { expiryStatus, formatQuantity } from './rules';

type Props = {
  item: InventoryItem;
  category: Category | undefined;
  today: string;
  canModify: boolean;
  // Membres du foyer (ordre d'arrivée) : une couleur d'avatar différente pour chacun.
  memberIds?: readonly string[];
  onPress: () => void;
  onConsume: () => void;
  onDiscard: () => void;
};

/**
 * Un produit du frigo, en carte : liseré et badge de la couleur de son urgence, catégorie,
 * produit perso, et les actions « Mangé » (couverts) / « Jeté » (poubelle) si l'utilisateur
 * peut le modifier. Le liseré aide à repérer les états en faisant défiler ; l'information
 * reste portée par le badge (fond + icône + libellé), jamais par la couleur seule.
 */
export function InventoryRow({ item, category, today, canModify, memberIds, onPress, onConsume, onDiscard }: Props) {
  const theme = useTheme();
  const styles = useStyles();
  const status = expiryStatus(item.expiresOn, item.expiryKind, today);
  const accent = theme.urgency[brandUrgency(status.urgency)].accent;
  // Le badge dit déjà « À vérifier » : le détail rappelle qu'une DDM dépassée reste souvent bonne.
  const detail = status.urgency === 'check' ? 'DDM dépassée : encore bon ?' : status.label;

  return (
    <Card onPress={onPress} accessibilityLabel={`${item.name}, ${status.label}`} style={styles.card}>
      <View style={[styles.stripe, { backgroundColor: accent }]} />

      <View style={styles.header}>
        <View style={styles.iconTile}>
          <CategoryIcon code={category?.code} />
        </View>
        <View style={styles.titles}>
          <Text style={styles.name} numberOfLines={2}>
            {item.name}
          </Text>
          <Text style={styles.meta} numberOfLines={1}>
            {formatQuantity(item.quantity, item.unit)}
            {category ? ` · ${category.name}` : ''}
          </Text>
        </View>
        {canModify ? (
          <View style={styles.actions}>
            <IconButton icon={UtensilsCrossed} onPress={onConsume} accessibilityLabel={`${item.name} : consommé`} />
            <IconButton icon={Trash} variant="secondary" onPress={onDiscard} accessibilityLabel={`${item.name} : jeté`} />
          </View>
        ) : null}
      </View>

      {/* Sous le nom, sur toute la largeur restante : le détail tient souvent sur une ligne. */}
      <View style={styles.body}>
        <View style={styles.badges}>
          <UrgencyBadge urgency={status.urgency} />
          {item.ownerUserId ? (
            <PersoBadge ownerUserId={item.ownerUserId} ownerDisplayName={item.ownerDisplayName} memberIds={memberIds} />
          ) : null}
        </View>
        <Text style={styles.detail}>
          {detail}
          {item.expiryIsEstimated ? ' (date estimée)' : ''}
        </Text>
      </View>
    </Card>
  );
}

const ICON_TILE = 44;
const STRIPE_WIDTH = 5;

const useStyles = makeStyles((t) => ({
  // overflow hidden : le liseré suit l'arrondi de la carte.
  card: { gap: t.space.xs, overflow: 'hidden', paddingLeft: t.space.md + STRIPE_WIDTH },
  stripe: { position: 'absolute', left: 0, top: 0, bottom: 0, width: STRIPE_WIDTH },
  header: { flexDirection: 'row', alignItems: 'flex-start', gap: t.space.sm },
  iconTile: {
    width: ICON_TILE,
    height: ICON_TILE,
    borderRadius: t.radius.sm,
    backgroundColor: t.colors.surface2,
    alignItems: 'center',
    justifyContent: 'center',
  },
  titles: { flex: 1, gap: t.space.xxs },
  name: { ...t.type.card, color: t.colors.ink },
  meta: { ...t.type.caption, color: t.colors.ink3 },
  actions: { flexDirection: 'row', gap: t.space.xs },
  // Aligné sous le nom (largeur de l'icône de catégorie + espacement).
  body: { marginLeft: ICON_TILE + t.space.sm, gap: t.space.xxs },
  badges: { flexDirection: 'row', flexWrap: 'wrap', gap: t.space.xxs },
  detail: { ...t.type.caption, color: t.colors.ink2 },
}));

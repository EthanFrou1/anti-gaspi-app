import { Text, View } from 'react-native';
import type { Category, InventoryItem } from '@/api/types';
import { Button } from '@/components/Button';
import { Card } from '@/components/Card';
import { CategoryIcon } from '@/components/CategoryIcon';
import { PersoBadge } from '@/components/PersoBadge';
import { UrgencyBadge } from '@/components/UrgencyBadge';
import { makeStyles } from '@/theme';
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
 * Un produit du frigo, en carte : catégorie, urgence (badge de la charte + détail), produit
 * perso, et les actions « Mangé » / « Jeté » si l'utilisateur peut le modifier.
 */
export function InventoryRow({ item, category, today, canModify, memberIds, onPress, onConsume, onDiscard }: Props) {
  const styles = useStyles();
  const status = expiryStatus(item.expiresOn, item.expiryKind, today);
  // Le badge dit déjà « À vérifier » : le détail rappelle qu'une DDM dépassée reste souvent bonne.
  const detail = status.urgency === 'check' ? 'DDM dépassée : encore bon ?' : status.label;

  return (
    <Card onPress={onPress} accessibilityLabel={`${item.name}, ${status.label}`} style={styles.card}>
      <View style={styles.iconTile}>
        <CategoryIcon code={category?.code} />
      </View>

      <View style={styles.content}>
        <Text style={styles.name} numberOfLines={2}>
          {item.name}
        </Text>
        <Text style={styles.meta} numberOfLines={1}>
          {formatQuantity(item.quantity, item.unit)}
          {category ? ` · ${category.name}` : ''}
        </Text>
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

      {canModify ? (
        <View style={styles.actions}>
          <Button title="Mangé" size="small" onPress={onConsume} accessibilityLabel={`${item.name} : consommé`} />
          <Button title="Jeté" size="small" variant="secondary" onPress={onDiscard} accessibilityLabel={`${item.name} : jeté`} />
        </View>
      ) : null}
    </Card>
  );
}

const useStyles = makeStyles((t) => ({
  card: { flexDirection: 'row', alignItems: 'flex-start', gap: t.space.sm },
  iconTile: {
    width: 44,
    height: 44,
    borderRadius: t.radius.sm,
    backgroundColor: t.colors.surface2,
    alignItems: 'center',
    justifyContent: 'center',
  },
  content: { flex: 1, gap: t.space.xxs },
  name: { ...t.type.card, color: t.colors.ink },
  meta: { ...t.type.caption, color: t.colors.ink3 },
  badges: { flexDirection: 'row', flexWrap: 'wrap', gap: t.space.xxs, marginTop: 2 },
  detail: { ...t.type.caption, color: t.colors.ink2 },
  // Largeur fixe : les deux boutons ont la même taille, d'une ligne à l'autre.
  actions: { width: 88, gap: t.space.xs },
}));

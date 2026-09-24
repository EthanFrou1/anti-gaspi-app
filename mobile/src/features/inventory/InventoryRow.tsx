import { Pressable, StyleSheet, Text, View } from 'react-native';
import type { Category, InventoryItem } from '@/api/types';
import { Button } from '@/components/Button';
import { colors, spacing } from '@/theme';
import { expiryStatus, formatQuantity, type Urgency } from './rules';

type Props = {
  item: InventoryItem;
  category: Category | undefined;
  today: string;
  canModify: boolean;
  onPress: () => void;
  onConsume: () => void;
  onDiscard: () => void;
};

// Couleurs provisoires (la DA viendra plus tard). Une DDM dépassée est en orange
// « à vérifier », jamais en rouge « périmé ».
const URGENCY_COLORS: Record<Urgency, string> = {
  expired: '#C62828',
  check: '#EF6C00',
  critical: '#E65100',
  soon: '#F9A825',
  ok: '#2E7D32',
};

export function InventoryRow({ item, category, today, canModify, onPress, onConsume, onDiscard }: Props) {
  const status = expiryStatus(item.expiresOn, item.expiryKind, today);
  const color = URGENCY_COLORS[status.urgency];

  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={`${item.name}, ${status.label}`}
      style={({ pressed }) => [styles.row, pressed && styles.pressed]}
    >
      <View style={[styles.urgencyBar, { backgroundColor: color }]} />
      <View style={styles.content}>
        <View style={styles.titleRow}>
          <Text style={styles.name} numberOfLines={1}>
            {item.name}
          </Text>
          {item.ownerDisplayName ? <Text style={styles.personal}>Perso · {item.ownerDisplayName}</Text> : null}
        </View>
        <Text style={styles.meta} numberOfLines={1}>
          {formatQuantity(item.quantity, item.unit)}
          {category ? ` · ${category.name}` : ''}
        </Text>
        <Text style={[styles.expiry, { color }]}>
          {status.label}
          {item.expiryIsEstimated ? ' (estimée)' : ''}
        </Text>
      </View>
      {canModify ? (
        <View style={styles.actions}>
          <Button title="Mangé" size="small" onPress={onConsume} accessibilityLabel={`${item.name} : consommé`} />
          <Button title="Jeté" size="small" variant="secondary" onPress={onDiscard} accessibilityLabel={`${item.name} : jeté`} />
        </View>
      ) : null}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.background,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: colors.border,
    paddingRight: spacing.md,
  },
  pressed: { opacity: 0.7 },
  urgencyBar: { width: 5, alignSelf: 'stretch' },
  content: { flex: 1, paddingVertical: spacing.sm + 2, paddingHorizontal: spacing.md, gap: 2 },
  titleRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
  name: { flexShrink: 1, fontSize: 16, fontWeight: '600', color: colors.text },
  personal: {
    fontSize: 11,
    color: colors.mutedText,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 4,
    paddingHorizontal: 4,
  },
  meta: { fontSize: 13, color: colors.mutedText },
  expiry: { fontSize: 13, fontWeight: '600' },
  // Largeur fixe : les deux boutons ont la même taille, d'une ligne à l'autre.
  actions: { width: 88, gap: spacing.xs, paddingVertical: spacing.sm },
});

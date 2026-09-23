import { useState } from 'react';
import { FlatList, Modal, Pressable, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import type { Category } from '@/api/types';
import { colors, spacing } from '@/theme';

type Props = {
  categories: Category[];
  value: number | null;
  onChange: (categoryId: number) => void;
  error?: string;
  disabled?: boolean;
};

/**
 * Choix de la catégorie : 26 options, trop pour des pastilles.
 * Une liste plein écran affiche aussi la durée par défaut et le type de date.
 */
export function CategoryPicker({ categories, value, onChange, error, disabled }: Props) {
  const [open, setOpen] = useState(false);
  const selected = categories.find((c) => c.id === value);

  return (
    <View style={styles.container}>
      <Text style={styles.label}>Catégorie</Text>
      <Pressable
        onPress={() => setOpen(true)}
        disabled={disabled}
        accessibilityRole="button"
        accessibilityLabel={`Catégorie : ${selected?.name ?? 'à choisir'}`}
        style={[styles.field, error ? styles.fieldError : null, disabled && styles.disabled]}
      >
        <Text style={selected ? styles.value : styles.placeholder}>{selected?.name ?? 'Choisir une catégorie'}</Text>
        <Text style={styles.chevron}>›</Text>
      </Pressable>
      {error ? <Text style={styles.error}>{error}</Text> : null}

      <Modal visible={open} animationType="slide" onRequestClose={() => setOpen(false)}>
        <SafeAreaView style={styles.modal}>
          <View style={styles.modalHeader}>
            <Text style={styles.modalTitle}>Catégorie</Text>
            <Pressable onPress={() => setOpen(false)} accessibilityRole="button" hitSlop={8}>
              <Text style={styles.close}>Fermer</Text>
            </Pressable>
          </View>
          <FlatList
            data={categories}
            keyExtractor={(c) => String(c.id)}
            renderItem={({ item }) => (
              <Pressable
                onPress={() => {
                  onChange(item.id);
                  setOpen(false);
                }}
                accessibilityRole="button"
                style={[styles.option, item.id === value && styles.optionSelected]}
              >
                <Text style={styles.optionName}>{item.name}</Text>
                <Text style={styles.optionMeta}>
                  ≈ {formatDuration(item.defaultShelfLifeDays)} · {item.expiryKind === 'UseBy' ? 'DLC' : 'DDM'}
                </Text>
              </Pressable>
            )}
          />
        </SafeAreaView>
      </Modal>
    </View>
  );
}

function formatDuration(days: number): string {
  if (days >= 365) return `${Math.round(days / 365)} an${days >= 730 ? 's' : ''}`;
  if (days >= 60) return `${Math.round(days / 30)} mois`;
  if (days >= 14) return `${Math.round(days / 7)} sem.`;
  return `${days} j`;
}

const styles = StyleSheet.create({
  container: { gap: spacing.xs },
  label: { fontSize: 14, fontWeight: '600', color: colors.text },
  field: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 8,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm + 4,
  },
  fieldError: { borderColor: colors.error },
  disabled: { opacity: 0.6 },
  value: { fontSize: 16, color: colors.text },
  placeholder: { fontSize: 16, color: colors.mutedText },
  chevron: { fontSize: 20, color: colors.mutedText },
  error: { fontSize: 13, color: colors.error },
  modal: { flex: 1, backgroundColor: colors.background },
  modalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: spacing.md,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: colors.border,
  },
  modalTitle: { fontSize: 18, fontWeight: '700', color: colors.text },
  close: { fontSize: 16, color: colors.primary, fontWeight: '600' },
  option: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.md,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: colors.border,
  },
  optionSelected: { backgroundColor: '#E8F5E9' },
  optionName: { fontSize: 16, color: colors.text },
  optionMeta: { fontSize: 13, color: colors.mutedText },
});

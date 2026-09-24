import { useState } from 'react';
import { FlatList, Modal, Pressable, Text, View } from 'react-native';
import { SafeAreaProvider, SafeAreaView } from 'react-native-safe-area-context';
import type { Category } from '@/api/types';
import { CategoryIcon } from '@/components/CategoryIcon';
import { useFieldStyles } from '@/components/TextField';
import { makeStyles } from '@/theme';

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
  const field = useFieldStyles();
  const styles = useStyles();
  const [open, setOpen] = useState(false);
  const selected = categories.find((c) => c.id === value);

  return (
    <View style={field.container}>
      <Text style={field.label}>Catégorie</Text>
      <Pressable
        onPress={() => setOpen(true)}
        disabled={disabled}
        accessibilityRole="button"
        accessibilityLabel={`Catégorie : ${selected?.name ?? 'à choisir'}`}
        style={[field.input, styles.field, error ? field.inputError : null, disabled && styles.disabled]}
      >
        {selected ? <CategoryIcon code={selected.code} size={22} /> : null}
        <Text style={[selected ? styles.value : styles.placeholder, styles.fieldText]}>{selected?.name ?? 'Choisir une catégorie'}</Text>
        <Text style={styles.chevron}>›</Text>
      </Pressable>
      {error ? <Text style={field.error}>{error}</Text> : null}

      {/*
        pageSheet (iOS) : la liste s'ouvre en « feuille » sous la barre d'état, et se ferme aussi
        en la faisant glisser vers le bas (onRequestClose est alors appelé). Ignoré sur Android.
      */}
      <Modal visible={open} animationType="slide" presentationStyle="pageSheet" onRequestClose={() => setOpen(false)}>
        {/*
          Une Modal s'affiche dans sa propre fenêtre native : sans son propre SafeAreaProvider,
          les marges de l'encoche y valent 0 et l'en-tête passe sous la barre d'état.
        */}
        <SafeAreaProvider>
          <SafeAreaView style={styles.modal}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>Catégorie</Text>
              <Pressable onPress={() => setOpen(false)} accessibilityRole="button" hitSlop={12} style={styles.closeButton}>
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
                  accessibilityState={{ selected: item.id === value }}
                  style={({ pressed }) => [styles.option, item.id === value && styles.optionSelected, pressed && styles.optionPressed]}
                >
                  <CategoryIcon code={item.code} />
                  <View style={styles.optionText}>
                    <Text style={styles.optionName}>{item.name}</Text>
                    <Text style={styles.optionMeta}>
                      ≈ {formatDuration(item.defaultShelfLifeDays)} · {item.expiryKind === 'UseBy' ? 'DLC' : 'DDM'}
                    </Text>
                  </View>
                  {item.id === value ? <Text style={styles.check}>✓</Text> : null}
                </Pressable>
              )}
            />
          </SafeAreaView>
        </SafeAreaProvider>
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

const useStyles = makeStyles((t) => ({
  field: { flexDirection: 'row', alignItems: 'center', gap: t.space.xs },
  fieldText: { flex: 1 },
  disabled: { opacity: 0.6 },
  value: { ...t.type.body, color: t.colors.ink },
  placeholder: { ...t.type.body, color: t.colors.ink3 },
  chevron: { ...t.type.title3, color: t.colors.ink3 },
  modal: { flex: 1, backgroundColor: t.colors.bg },
  modalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: t.layout.screenPadding,
    paddingVertical: t.space.md,
    borderBottomWidth: t.borderWidth.hairline,
    borderBottomColor: t.colors.line,
  },
  modalTitle: { ...t.type.title3, color: t.colors.ink },
  close: { ...t.type.bodyBold, color: t.colors.primaryText },
  closeButton: { minHeight: t.layout.minTouch, justifyContent: 'center' },
  option: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: t.space.sm,
    minHeight: t.layout.minTouch + t.space.md,
    paddingHorizontal: t.layout.screenPadding,
    paddingVertical: t.space.sm,
    borderBottomWidth: t.borderWidth.hairline,
    borderBottomColor: t.colors.line,
  },
  // Sélection : coche + fond doux (jamais la couleur seule).
  optionSelected: { backgroundColor: t.colors.primarySoft },
  optionPressed: { backgroundColor: t.colors.surface2 },
  optionText: { flex: 1, gap: 2 },
  optionName: { ...t.type.body, color: t.colors.ink },
  optionMeta: { ...t.type.caption, color: t.colors.ink3 },
  check: { ...t.type.bodyBold, color: t.colors.ink },
}));

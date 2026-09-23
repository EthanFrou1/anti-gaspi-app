import { Pressable, StyleSheet, Text, View } from 'react-native';
import { colors, spacing } from '@/theme';

type Option<T extends string> = { value: T; label: string };

type Props<T extends string> = {
  options: Option<T>[];
  values: readonly T[];
  onToggle: (value: T) => void;
  disabled?: boolean;
};

/**
 * Choix multiple sous forme de pastilles (allergies, exclusions, équipement…).
 */
export function MultiChoiceChips<T extends string>({ options, values, onToggle, disabled = false }: Props<T>) {
  return (
    <View style={styles.row}>
      {options.map((option) => {
        const selected = values.includes(option.value);
        return (
          <Pressable
            key={option.value}
            onPress={() => onToggle(option.value)}
            disabled={disabled}
            accessibilityRole="checkbox"
            accessibilityState={{ checked: selected, disabled }}
            style={[styles.chip, selected && styles.selected, disabled && styles.disabled]}
          >
            <Text style={[styles.label, selected && styles.selectedLabel]}>
              {selected ? '✓ ' : ''}
              {option.label}
            </Text>
          </Pressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  chip: {
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 16,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.xs + 2,
  },
  selected: { backgroundColor: colors.primary, borderColor: colors.primary },
  disabled: { opacity: 0.5 },
  label: { fontSize: 14, color: colors.text },
  selectedLabel: { color: colors.primaryText, fontWeight: '600' },
});

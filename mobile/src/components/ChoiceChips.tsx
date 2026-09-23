import { Pressable, StyleSheet, Text, View } from 'react-native';
import { colors, spacing } from '@/theme';

type Option<T extends string> = { value: T; label: string };

type Props<T extends string> = {
  options: Option<T>[];
  value: T | null;
  onChange: (value: T) => void;
  disabled?: boolean;
};

/**
 * Choix unique parmi quelques options, sous forme de pastilles (unités, filtres…).
 * Générique : T est le type des valeurs possibles (ex. QuantityUnit).
 */
export function ChoiceChips<T extends string>({ options, value, onChange, disabled = false }: Props<T>) {
  return (
    <View style={styles.row} accessibilityRole="radiogroup">
      {options.map((option) => {
        const selected = option.value === value;
        return (
          <Pressable
            key={option.value}
            onPress={() => onChange(option.value)}
            disabled={disabled}
            accessibilityRole="radio"
            accessibilityState={{ selected, disabled }}
            style={[styles.chip, selected && styles.selected, disabled && styles.disabled]}
          >
            <Text style={[styles.label, selected && styles.selectedLabel]}>{option.label}</Text>
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

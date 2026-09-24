import { Pressable, Text, type AccessibilityRole } from 'react-native';
import { makeStyles } from '@/theme';

type Props = {
  label: string;
  selected: boolean;
  onPress: () => void;
  disabled?: boolean;
  accessibilityRole: AccessibilityRole;
};

/**
 * Pastille de la charte. Sélectionnée : bord plus épais et plus foncé, coche et fond doux —
 * jamais la couleur seule. Base commune de ChoiceChips et MultiChoiceChips.
 */
export function Chip({ label, selected, onPress, disabled = false, accessibilityRole }: Props) {
  const styles = useStyles();
  const state = accessibilityRole === 'checkbox' ? { checked: selected, disabled } : { selected, disabled };
  return (
    <Pressable
      onPress={onPress}
      disabled={disabled}
      accessibilityRole={accessibilityRole}
      accessibilityState={state}
      hitSlop={4}
      style={({ pressed }) => [styles.chip, selected && styles.selected, pressed && styles.pressed, disabled && styles.disabled]}
    >
      <Text style={styles.label}>
        {selected ? '✓ ' : ''}
        {label}
      </Text>
    </Pressable>
  );
}

const useStyles = makeStyles((t) => ({
  chip: {
    minHeight: 40,
    justifyContent: 'center',
    borderWidth: t.borderWidth.hairline,
    borderColor: t.colors.line,
    backgroundColor: t.colors.surface,
    borderRadius: t.radius.pill,
    paddingHorizontal: t.space.md,
  },
  selected: {
    borderWidth: t.borderWidth.selected,
    borderColor: t.colors.border,
    backgroundColor: t.colors.primarySoft,
    // Le bord plus épais ne doit pas faire bouger le texte.
    paddingHorizontal: t.space.md - (t.borderWidth.selected - t.borderWidth.hairline),
  },
  pressed: { opacity: 0.8 },
  disabled: { opacity: 0.45 },
  label: { ...t.type.callout, color: t.colors.ink },
}));

import type { ReactNode } from 'react';
import { Pressable, Text, type AccessibilityRole } from 'react-native';
import { makeStyles } from '@/theme';

type Props = {
  label: string;
  selected: boolean;
  onPress: () => void;
  disabled?: boolean;
  accessibilityRole: AccessibilityRole;
  // Icône facultative devant le libellé (ex. icône d'état dans les filtres du frigo).
  icon?: ReactNode;
  // Libellé lu par VoiceOver / TalkBack, si le texte affiché ne suffit pas (« Urgent, 5 produits »).
  accessibilityLabel?: string;
};

/**
 * Pastille de la charte. Sélectionnée : bord plus épais et plus foncé, coche et fond doux —
 * jamais la couleur seule. Base commune de ChoiceChips et MultiChoiceChips.
 */
export function Chip({ label, selected, onPress, disabled = false, accessibilityRole, icon, accessibilityLabel }: Props) {
  const styles = useStyles();
  const state = accessibilityRole === 'checkbox' ? { checked: selected, disabled } : { selected, disabled };
  return (
    <Pressable
      onPress={onPress}
      disabled={disabled}
      accessibilityRole={accessibilityRole}
      accessibilityState={state}
      accessibilityLabel={accessibilityLabel}
      hitSlop={4}
      style={({ pressed }) => [styles.chip, selected && styles.selected, pressed && styles.pressed, disabled && styles.disabled]}
    >
      {selected ? <Text style={styles.label}>✓</Text> : null}
      {icon}
      <Text style={styles.label}>{label}</Text>
    </Pressable>
  );
}

const useStyles = makeStyles((t) => ({
  chip: {
    minHeight: 40,
    flexDirection: 'row',
    alignItems: 'center',
    gap: t.space.xxs,
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

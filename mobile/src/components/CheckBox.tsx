import { View } from 'react-native';
import { makeStyles, useTheme } from '@/theme';
import { Check } from './icons/lucide';

type Props = {
  checked: boolean;
  disabled?: boolean;
};

/**
 * Case à cocher (visuel seul : le Pressable qui l'entoure porte le rôle « checkbox » et son état).
 * Cochée : fond mandarine ET coche, jamais la couleur seule.
 */
export function CheckBox({ checked, disabled = false }: Props) {
  const theme = useTheme();
  const styles = useStyles();
  return (
    <View style={[styles.box, checked && styles.checked, disabled && styles.disabled]}>
      {checked ? <Check size={18} strokeWidth={3} color={theme.colors.onPrimary} /> : null}
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  box: {
    width: 28,
    height: 28,
    borderRadius: 8,
    borderWidth: t.borderWidth.selected,
    borderColor: t.colors.border,
    backgroundColor: t.colors.surface,
    alignItems: 'center',
    justifyContent: 'center',
  },
  checked: { backgroundColor: t.colors.primary },
  disabled: { opacity: 0.4 },
}));

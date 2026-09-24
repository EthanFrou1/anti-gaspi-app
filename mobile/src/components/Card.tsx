import type { ReactNode } from 'react';
import { Pressable, View, type StyleProp, type ViewStyle } from 'react-native';
import { makeStyles } from '@/theme';

type Props = {
  children: ReactNode;
  // Carte cliquable (ex. une ligne du frigo) : elle devient un bouton.
  onPress?: () => void;
  accessibilityLabel?: string;
  // Sélection = bord plus épais et plus foncé (jamais la couleur seule).
  selected?: boolean;
  style?: StyleProp<ViewStyle>;
};

/**
 * Carte « sobre » de la charte : fond de surface, trait fin, aucune ombre
 * (l'ombre est réservée aux boutons d'action).
 */
export function Card({ children, onPress, accessibilityLabel, selected = false, style }: Props) {
  const styles = useStyles();
  const cardStyle = [styles.card, selected && styles.selected, style];

  if (!onPress) {
    return <View style={cardStyle}>{children}</View>;
  }
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel}
      accessibilityState={{ selected }}
      style={({ pressed }) => [cardStyle, pressed && styles.pressed]}
    >
      {children}
    </Pressable>
  );
}

const useStyles = makeStyles((t) => ({
  card: {
    backgroundColor: t.colors.surface,
    borderWidth: t.borderWidth.hairline,
    borderColor: t.colors.line,
    borderRadius: t.radius.lg,
    padding: t.space.md,
  },
  selected: { borderWidth: t.borderWidth.selected, borderColor: t.colors.border },
  pressed: { backgroundColor: t.colors.surface2 },
}));

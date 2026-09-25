import { Text, View } from 'react-native';
import { TriangleAlert } from '@/components/icons/lucide';
import { makeStyles } from '@/theme';

type Props = {
  children: string;
  // Plus visible (bord épais) : avertissement renforcé en cas d'allergie.
  strong?: boolean;
};

/** Avertissement « vérifie les étiquettes » : fond citron doux, texte citron foncé (contraste AA). */
export function WarningNote({ children, strong = false }: Props) {
  const styles = useStyles();
  return (
    <View style={[styles.warning, strong && styles.strong]} accessibilityRole="alert">
      <TriangleAlert size={20} strokeWidth={2} color={styles.text.color} />
      <Text style={[styles.text, strong && styles.strongText]}>{children}</Text>
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  warning: {
    flexDirection: 'row',
    gap: t.space.xs,
    alignItems: 'flex-start',
    backgroundColor: t.scheme === 'dark' ? t.palette.citron.darkSoft : t.palette.citron.soft,
    padding: t.space.md,
    borderRadius: t.radius.sm,
  },
  strong: {
    borderWidth: t.borderWidth.selected,
    borderColor: t.scheme === 'dark' ? t.palette.citron.darkText : t.palette.citron.text,
  },
  text: { ...t.type.callout, flex: 1, color: t.scheme === 'dark' ? t.palette.citron.darkText : t.palette.citron.text },
  strongText: { ...t.type.bodyBold },
}));

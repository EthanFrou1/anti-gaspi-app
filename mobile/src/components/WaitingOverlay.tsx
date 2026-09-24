import { ActivityIndicator, Modal, Text, View } from 'react-native';
import { makeStyles, useTheme } from '@/theme';

type Props = {
  visible: boolean;
  title: string;
  text: string;
};

/**
 * Écran d'attente d'une opération longue (lecture d'un ticket, génération d'une recette) :
 * carte centrée sur un fond assombri, qui bloque l'écran le temps de l'appel.
 */
export function WaitingOverlay({ visible, title, text }: Props) {
  const theme = useTheme();
  const styles = useStyles();

  return (
    <Modal visible={visible} transparent animationType="fade">
      <View style={styles.overlay}>
        <View style={styles.card} accessibilityRole="progressbar" accessibilityLabel={`${title} ${text}`}>
          <ActivityIndicator size="large" color={theme.colors.primary} />
          <Text style={styles.title}>{title}</Text>
          <Text style={styles.text}>{text}</Text>
        </View>
      </View>
    </Modal>
  );
}

const useStyles = makeStyles((t) => ({
  overlay: { flex: 1, backgroundColor: t.colors.scrim, justifyContent: 'center', padding: t.layout.screenPadding },
  card: {
    backgroundColor: t.colors.surface,
    borderWidth: t.borderWidth.hairline,
    borderColor: t.colors.line,
    borderRadius: t.radius.xl,
    padding: t.space.xl,
    gap: t.space.sm,
    alignItems: 'center',
  },
  title: { ...t.type.title3, color: t.colors.ink, textAlign: 'center' },
  text: { ...t.type.body, color: t.colors.ink2, textAlign: 'center' },
}));

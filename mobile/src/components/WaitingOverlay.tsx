import { useEffect, useRef } from 'react';
import { AccessibilityInfo, ActivityIndicator, Animated, Easing, Modal, Text, View } from 'react-native';
import { makeStyles, useTheme } from '@/theme';
import { Illustration, type IllustrationName } from './Illustration';

type Props = {
  visible: boolean;
  title: string;
  text: string;
  // Illustration de la charte au-dessus du texte (ex. la marmite pendant la génération d'une recette).
  illustration?: IllustrationName;
};

/**
 * Écran d'attente d'une opération longue (lecture d'un ticket, génération d'une recette) :
 * carte centrée sur un fond assombri, qui bloque l'écran le temps de l'appel.
 */
export function WaitingOverlay({ visible, title, text, illustration }: Props) {
  const theme = useTheme();
  const styles = useStyles();
  const bob = useBobbing(visible && illustration !== undefined);

  return (
    <Modal visible={visible} transparent animationType="fade">
      <View style={styles.overlay}>
        <View style={styles.card} accessibilityRole="progressbar" accessibilityLabel={`${title} ${text}`}>
          {illustration ? (
            <Animated.View style={{ transform: [{ translateY: bob }] }}>
              <Illustration name={illustration} size={96} />
            </Animated.View>
          ) : null}
          <ActivityIndicator size={illustration ? 'small' : 'large'} color={theme.colors.primary} />
          <Text style={styles.title}>{title}</Text>
          <Text style={styles.text}>{text}</Text>
        </View>
      </View>
    </Modal>
  );
}

/** Léger balancement vertical en boucle (6 points), sauf si l'utilisateur réduit les animations. */
function useBobbing(active: boolean) {
  const value = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    if (!active) return;
    let loop: Animated.CompositeAnimation | undefined;
    let cancelled = false;
    void AccessibilityInfo.isReduceMotionEnabled().then((reduceMotion) => {
      if (cancelled || reduceMotion) return;
      const step = (toValue: number) =>
        Animated.timing(value, { toValue, duration: 700, easing: Easing.inOut(Easing.sin), useNativeDriver: true });
      loop = Animated.loop(Animated.sequence([step(-6), step(0)]));
      loop.start();
    });
    return () => {
      cancelled = true;
      loop?.stop();
      value.setValue(0);
    };
  }, [active, value]);

  return value;
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

import { useEffect, useMemo, useRef } from 'react';
import { AccessibilityInfo, Animated, Easing, Modal, Pressable, Text, View } from 'react-native';
import { Illustration } from '@/components/Illustration';
import { makeStyles, useTheme } from '@/theme';
import { confettiPieces } from './confetti';

type Props = {
  // Nom du produit sauvé ; null = célébration masquée.
  productName: string | null;
  onDone: () => void;
};

// Durée d'affichage totale (l'animation elle-même dure 800 ms, tokens motion.celebrate).
const DISPLAY_MS = 1800;

/**
 * Célébration « Produit sauvé » après « Mangé » : la mascotte ravie apparaît au milieu d'une
 * gerbe de confettis, puis tout disparaît seul (ou d'un appui). Purement visuelle.
 * « Réduire les animations » : ni confettis ni rebond, seulement le message.
 */
export function SavedCelebration({ productName, onDone }: Props) {
  const theme = useTheme();
  const styles = useStyles();
  const visible = productName !== null;
  const progress = useRef(new Animated.Value(0)).current;
  // Toujours la dernière version de onDone, sans relancer l'animation quand elle change.
  const onDoneRef = useRef(onDone);
  onDoneRef.current = onDone;

  const pieces = useMemo(() => {
    const { tangerine, citron, raisin, framboise, myrtille } = theme.palette;
    return confettiPieces(18, [tangerine.base, citron.base, raisin.base, framboise.base, myrtille.base]);
  }, [theme.palette]);

  useEffect(() => {
    if (!visible) return;
    progress.setValue(0);
    AccessibilityInfo.announceForAccessibility(`Produit sauvé : ${productName} !`);

    let timer: ReturnType<typeof setTimeout> | undefined;
    let cancelled = false;
    void AccessibilityInfo.isReduceMotionEnabled().then((reduceMotion) => {
      if (cancelled) return;
      Animated.timing(progress, {
        toValue: 1,
        duration: reduceMotion ? 0 : theme.motion.celebrate.duration,
        easing: Easing.out(Easing.cubic),
        useNativeDriver: true,
      }).start();
      timer = setTimeout(() => onDoneRef.current(), DISPLAY_MS);
    });
    return () => {
      cancelled = true;
      if (timer) clearTimeout(timer);
    };
  }, [visible, productName, progress, theme.motion.celebrate.duration]);

  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onDone}>
      <Pressable style={styles.overlay} onPress={onDone} accessibilityRole="button" accessibilityLabel="Fermer">
        <View style={styles.center}>
          {pieces.map((piece, index) => (
            <Animated.View
              key={index}
              style={[
                styles.piece,
                {
                  width: piece.size,
                  height: piece.round ? piece.size : piece.size / 2,
                  borderRadius: piece.round ? piece.size / 2 : 2,
                  backgroundColor: piece.color,
                  opacity: progress.interpolate({ inputRange: [0, 0.7, 1], outputRange: [1, 1, 0] }),
                  transform: [
                    { translateX: progress.interpolate({ inputRange: [0, 1], outputRange: [0, piece.dx] }) },
                    { translateY: progress.interpolate({ inputRange: [0, 1], outputRange: [0, piece.dy] }) },
                    { rotate: progress.interpolate({ inputRange: [0, 1], outputRange: ['0deg', `${piece.rotate}deg`] }) },
                  ],
                },
              ]}
            />
          ))}
          <Animated.View
            style={[
              styles.card,
              { transform: [{ scale: progress.interpolate({ inputRange: [0, 0.6, 1], outputRange: [0.6, 1.08, 1] }) }] },
            ]}
          >
            <Illustration name="mascotDelighted" size={104} />
            <Text style={styles.title}>Produit sauvé !</Text>
            <Text style={styles.text}>{productName} ne finira pas à la poubelle.</Text>
          </Animated.View>
        </View>
      </Pressable>
    </Modal>
  );
}

const useStyles = makeStyles((t) => ({
  overlay: { flex: 1, backgroundColor: t.colors.scrim, justifyContent: 'center', padding: t.layout.screenPadding },
  center: { alignItems: 'center', justifyContent: 'center' },
  piece: { position: 'absolute' },
  card: {
    alignItems: 'center',
    gap: t.space.xs,
    backgroundColor: t.colors.surface,
    borderWidth: t.borderWidth.hairline,
    borderColor: t.colors.line,
    borderRadius: t.radius.xl,
    paddingVertical: t.space.xl,
    paddingHorizontal: t.space.xl,
  },
  title: { ...t.type.title2, color: t.colors.ink, textAlign: 'center' },
  text: { ...t.type.body, color: t.colors.ink2, textAlign: 'center' },
}));

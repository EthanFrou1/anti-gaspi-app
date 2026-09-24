import { useEffect, useRef, useState, type ReactNode } from 'react';
import { AccessibilityInfo, Animated, Modal, Pressable, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { makeStyles, useTheme } from '@/theme';

type Props = {
  visible: boolean;
  title: string;
  // Fermeture demandée (fond assombri, bouton retour d'Android).
  onClose: () => void;
  // Appelé une fois le panneau entièrement refermé (fin de l'animation). Utile pour naviguer
  // APRÈS la fermeture : ouvrir un écran pendant qu'une Modal se ferme pose problème sur iOS.
  onHidden?: () => void;
  children: ReactNode;
};

/**
 * Panneau qui monte depuis le bas de l'écran, sur un fond assombri (charte : trait fin en
 * haut, rayon 28, pas d'ombre). Animation de 280 ms, supprimée si le téléphone demande de
 * réduire les animations.
 */
export function BottomSheet({ visible, title, onClose, onHidden, children }: Props) {
  const theme = useTheme();
  const styles = useStyles();
  const insets = useSafeAreaInsets();
  // La Modal reste affichée pendant l'animation de fermeture.
  const [mounted, setMounted] = useState(visible);
  const progress = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    if (visible) setMounted(true);
  }, [visible]);

  useEffect(() => {
    if (!mounted) return;
    let cancelled = false;
    void AccessibilityInfo.isReduceMotionEnabled().then((reduceMotion) => {
      if (cancelled) return;
      Animated.timing(progress, {
        toValue: visible ? 1 : 0,
        duration: reduceMotion ? 0 : theme.motion.sheet.duration,
        useNativeDriver: true,
      }).start(({ finished }) => {
        if (finished && !visible) {
          setMounted(false);
          onHidden?.();
        }
      });
    });
    return () => {
      cancelled = true;
    };
    // onHidden volontairement absent des dépendances : seule l'ouverture ou la fermeture
    // relance l'animation.
  }, [visible, mounted, progress, theme.motion.sheet.duration]);

  return (
    <Modal visible={mounted} transparent animationType="none" onRequestClose={onClose} statusBarTranslucent>
      <Animated.View style={[styles.scrim, { opacity: progress }]}>
        <Pressable style={styles.fill} onPress={onClose} accessibilityRole="button" accessibilityLabel="Fermer" />
      </Animated.View>
      <Animated.View
        accessibilityViewIsModal
        style={[
          styles.sheet,
          { paddingBottom: insets.bottom + theme.space.md },
          { transform: [{ translateY: progress.interpolate({ inputRange: [0, 1], outputRange: [400, 0] }) }] },
        ]}
      >
        <View style={styles.handle} />
        <Text style={styles.title} accessibilityRole="header">
          {title}
        </Text>
        {children}
      </Animated.View>
    </Modal>
  );
}

const useStyles = makeStyles((t) => ({
  scrim: { position: 'absolute', top: 0, bottom: 0, left: 0, right: 0, backgroundColor: t.colors.scrim },
  fill: { flex: 1 },
  sheet: {
    position: 'absolute',
    left: 0,
    right: 0,
    bottom: 0,
    backgroundColor: t.colors.surface,
    borderTopLeftRadius: t.radius.sheet,
    borderTopRightRadius: t.radius.sheet,
    borderTopWidth: t.borderWidth.hairline,
    borderLeftWidth: t.borderWidth.hairline,
    borderRightWidth: t.borderWidth.hairline,
    borderColor: t.colors.line,
    paddingHorizontal: t.layout.screenPadding,
    paddingTop: t.space.xs,
    gap: t.space.sm,
  },
  handle: { alignSelf: 'center', width: 40, height: 5, borderRadius: 3, backgroundColor: t.colors.line, marginBottom: t.space.xs },
  title: { ...t.type.title3, color: t.colors.ink },
}));

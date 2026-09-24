import type { LucideIcon } from 'lucide-react-native';
import { Animated, Pressable, View } from 'react-native';
import { makeStyles, useTheme } from '@/theme';
import { useStickerPress } from './useStickerPress';

type Props = {
  icon: LucideIcon;
  onPress: () => void;
  // Obligatoire : sans texte visible, c'est le seul nom du bouton pour VoiceOver / TalkBack.
  accessibilityLabel: string;
  variant?: 'primary' | 'secondary';
};

/**
 * Bouton carré à icône seule, avec le bord encre et l'ombre « sticker » des boutons d'action
 * (même mécanique que Button : l'ombre reste en place, la face s'enfonce à l'appui).
 * Pour les actions répétées d'une liste, où un libellé prendrait trop de place.
 */
export function IconButton({ icon: Icon, onPress, accessibilityLabel, variant = 'primary' }: Props) {
  const theme = useTheme();
  const styles = useStyles();
  const { onPressIn, onPressOut, translateY } = useStickerPress();
  const primary = variant === 'primary';

  return (
    <Pressable
      onPress={onPress}
      onPressIn={onPressIn}
      onPressOut={onPressOut}
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel}
      style={styles.wrapper}
    >
      <View style={styles.shadow} />
      <Animated.View
        style={[
          styles.face,
          { backgroundColor: primary ? theme.colors.primary : theme.colors.surface },
          { transform: [{ translateY }] },
        ]}
      >
        <Icon size={22} strokeWidth={2.25} color={primary ? theme.colors.onPrimary : theme.colors.ink} />
      </Animated.View>
    </Pressable>
  );
}

const useStyles = makeStyles((t) => ({
  // La place de l'ombre est réservée sous le bouton : rien ne la recouvre.
  wrapper: { paddingBottom: t.shadow.button.offsetY },
  shadow: {
    position: 'absolute',
    top: t.shadow.button.offsetY,
    left: 0,
    width: t.layout.minTouch,
    height: t.layout.minTouch,
    borderRadius: t.radius.sm,
    backgroundColor: t.colors.shadow,
  },
  // 44 × 44 : la zone tactile minimale, sans hitSlop.
  face: {
    width: t.layout.minTouch,
    height: t.layout.minTouch,
    borderRadius: t.radius.sm,
    borderWidth: t.borderWidth.button,
    borderColor: t.colors.buttonBorder,
    alignItems: 'center',
    justifyContent: 'center',
  },
}));

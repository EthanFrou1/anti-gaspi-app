import { useRef } from 'react';
import { Animated } from 'react-native';
import { useTheme } from '@/theme';

/**
 * Effet « sticker » des boutons d'action : à l'appui, le bouton descend de la hauteur de son
 * ombre (4 px, 90 ms) et s'y enfonce ; il remonte au relâchement. Animation sur le fil natif.
 */
export function useStickerPress() {
  const theme = useTheme();
  // 0 = au repos, 1 = enfoncé.
  const press = useRef(new Animated.Value(0)).current;

  const animate = (toValue: number) =>
    Animated.timing(press, { toValue, duration: theme.motion.press.duration, useNativeDriver: true }).start();

  return {
    onPressIn: () => animate(1),
    onPressOut: () => animate(0),
    translateY: press.interpolate({ inputRange: [0, 1], outputRange: [0, theme.shadow.button.offsetY] }),
  };
}

import { useRef } from 'react';
import { ActivityIndicator, Animated, Pressable, Text, View } from 'react-native';
import { makeStyles, useTheme, type Theme } from '@/theme';

type ButtonVariant = 'primary' | 'secondary' | 'danger';

type ButtonProps = {
  title: string;
  onPress: () => void;
  loading?: boolean;
  disabled?: boolean;
  variant?: ButtonVariant;
};

/**
 * Bouton d'action de la charte : bord encre et ombre « sticker » (décalage net, sans flou),
 * réservée aux boutons d'action. Appuyé, il s'enfonce dans son ombre.
 *
 * L'ombre est un bloc plein placé sous le bouton et décalé vers le bas (solution des tokens
 * pour Android, où elevation floute toujours). On l'utilise aussi sur iOS : l'ombre native y
 * suivrait le bouton quand il s'enfonce, alors qu'elle doit rester en place. Même rendu partout.
 */
export function Button({ title, onPress, loading = false, disabled = false, variant = 'primary' }: ButtonProps) {
  const theme = useTheme();
  const styles = useStyles();
  const isDisabled = disabled || loading;
  const { background, foreground } = variantColors(theme, variant);
  const offset = theme.shadow.button.offsetY;

  // 0 = au repos, 1 = enfoncé (animation de 90 ms, sur le fil natif).
  const press = useRef(new Animated.Value(0)).current;
  const animate = (to: number) =>
    Animated.timing(press, { toValue: to, duration: theme.motion.press.duration, useNativeDriver: true }).start();

  return (
    <Pressable
      onPress={onPress}
      onPressIn={() => animate(1)}
      onPressOut={() => animate(0)}
      disabled={isDisabled}
      accessibilityRole="button"
      accessibilityLabel={title}
      accessibilityState={{ disabled: isDisabled, busy: loading }}
      style={styles.wrapper}
    >
      {isDisabled ? null : <View style={styles.shadow} />}
      <Animated.View
        style={[
          styles.face,
          { backgroundColor: background },
          isDisabled && styles.disabled,
          { transform: [{ translateY: press.interpolate({ inputRange: [0, 1], outputRange: [0, offset] }) }] },
        ]}
      >
        {loading ? (
          <ActivityIndicator color={foreground} />
        ) : (
          <Text style={[styles.text, { color: foreground }]}>{title}</Text>
        )}
      </Animated.View>
    </Pressable>
  );
}

function variantColors(theme: Theme, variant: ButtonVariant): { background: string; foreground: string } {
  switch (variant) {
    case 'secondary':
      return { background: theme.colors.surface, foreground: theme.colors.ink };
    case 'danger':
      return { background: theme.colors.danger, foreground: theme.colors.onDanger };
    default:
      return { background: theme.colors.primary, foreground: theme.colors.onPrimary };
  }
}

const useStyles = makeStyles((t) => ({
  // La place de l'ombre est réservée sous le bouton : rien ne la recouvre.
  wrapper: { paddingBottom: t.shadow.button.offsetY },
  shadow: {
    position: 'absolute',
    top: t.shadow.button.offsetY,
    bottom: 0,
    left: 0,
    right: 0,
    borderRadius: t.radius.md,
    backgroundColor: t.colors.shadow,
  },
  face: {
    minHeight: 52,
    borderRadius: t.radius.md,
    borderWidth: t.borderWidth.button,
    borderColor: t.colors.buttonBorder,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: t.space.lg,
    paddingVertical: t.space.xs,
  },
  disabled: { opacity: 0.45 },
  text: { ...t.type.button, textAlign: 'center' },
}));

import * as Haptics from 'expo-haptics';
import { useEffect, useRef } from 'react';
import { Animated, Easing, Pressable, Switch, Text, View } from 'react-native';
import type { DislikedFood } from '@/api/types';
import { Illustration } from '@/components/Illustration';
import { useReduceMotion } from '@/components/useReduceMotion';
import { makeStyles, useTheme } from '@/theme';
import { DISLIKE_OPTIONS, dislikesCountLabel, mascotMessage, type DislikeOption } from './dislikes';

type Props = {
  dislikes: readonly DislikedFood[];
  avoidSpicy: boolean;
  onToggleFood: (food: DislikedFood) => void;
  onAvoidSpicyChange: (value: boolean) => void;
};

/**
 * Grille « Pas pour moi » : on touche ce qu'on n'aime pas. La carte se secoue, s'incline et
 * reçoit le tampon « Pas pour moi » ; un second toucher la fait revenir avec un rebond. La
 * mandarine se balance et commente. « Réduire les animations » : seul l'état change.
 */
export function DislikesGrid({ dislikes, avoidSpicy, onToggleFood, onAvoidSpicyChange }: Props) {
  const theme = useTheme();
  const styles = useStyles();
  const reduceMotion = useReduceMotion();
  const sway = useRef(new Animated.Value(0)).current;

  function toggle(food: DislikedFood) {
    const selecting = !dislikes.includes(food);
    // Retour haptique : plus marqué quand on écarte un aliment. Sans effet si le téléphone ne vibre pas.
    void (selecting ? Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light) : Haptics.selectionAsync()).catch(() => {});
    if (!reduceMotion) {
      sway.setValue(0);
      Animated.timing(sway, { toValue: 1, duration: 480, easing: Easing.linear, useNativeDriver: true }).start();
    }
    onToggleFood(food);
  }

  return (
    <View style={styles.container}>
      <View style={styles.mascotRow}>
        <Animated.View
          style={{
            transform: [
              { rotate: sway.interpolate({ inputRange: [0, 0.25, 0.5, 0.75, 1], outputRange: ['0deg', '-8deg', '6deg', '-3deg', '0deg'] }) },
            ],
          }}
        >
          <Illustration name="mascotWelcoming" size={56} />
        </Animated.View>
        <View style={styles.bubble}>
          <Text style={styles.bubbleText}>{mascotMessage(dislikes.length)}</Text>
        </View>
      </View>

      <View style={styles.spicyRow}>
        <View style={styles.spicyText}>
          <Text style={styles.spicyLabel}>Pas épicé</Text>
          <Text style={styles.hint}>Ni piment ni épice piquante.</Text>
        </View>
        <Switch
          value={avoidSpicy}
          onValueChange={onAvoidSpicyChange}
          trackColor={{ true: theme.colors.primary, false: theme.colors.line }}
          ios_backgroundColor={theme.colors.line}
          accessibilityLabel="Pas épicé"
        />
      </View>

      <Text style={styles.count}>{dislikesCountLabel(dislikes.length)}</Text>
      <View style={styles.grid}>
        {DISLIKE_OPTIONS.map((option) => (
          <DislikeCard
            key={option.value}
            option={option}
            selected={dislikes.includes(option.value)}
            reduceMotion={reduceMotion}
            onPress={() => toggle(option.value)}
          />
        ))}
      </View>
    </View>
  );
}

type CardProps = {
  option: DislikeOption;
  selected: boolean;
  reduceMotion: boolean;
  onPress: () => void;
};

function DislikeCard({ option, selected, reduceMotion, onPress }: CardProps) {
  const styles = useStyles();
  // 0 = aimé, 1 = « Pas pour moi » (inclinaison, grisé, tampon).
  const state = useRef(new Animated.Value(selected ? 1 : 0)).current;
  const shake = useRef(new Animated.Value(0)).current;
  const bounce = useRef(new Animated.Value(1)).current;
  const previous = useRef(selected);

  useEffect(() => {
    if (previous.current === selected) return;
    previous.current = selected;
    if (reduceMotion) {
      state.setValue(selected ? 1 : 0);
      return;
    }
    if (selected) {
      // Petite secousse, puis la carte s'incline et le tampon tombe.
      shake.setValue(0);
      Animated.parallel([
        Animated.timing(shake, { toValue: 1, duration: 320, easing: Easing.linear, useNativeDriver: true }),
        Animated.timing(state, { toValue: 1, duration: 260, easing: Easing.out(Easing.back(2)), useNativeDriver: true }),
      ]).start();
    } else {
      // Retour avec un rebond.
      bounce.setValue(0.9);
      Animated.parallel([
        Animated.timing(state, { toValue: 0, duration: 180, useNativeDriver: true }),
        Animated.spring(bounce, { toValue: 1, friction: 3, tension: 180, useNativeDriver: true }),
      ]).start();
    }
  }, [selected, reduceMotion, state, shake, bounce]);

  const rotate = Animated.add(
    state.interpolate({ inputRange: [0, 1], outputRange: [0, -3] }),
    shake.interpolate({ inputRange: [0, 0.2, 0.4, 0.6, 0.8, 1], outputRange: [0, 5, -5, 3, -2, 0] }),
  ).interpolate({ inputRange: [-10, 10], outputRange: ['-10deg', '10deg'] });

  return (
    <Pressable
      onPress={onPress}
      style={styles.cell}
      accessibilityRole="checkbox"
      accessibilityState={{ checked: selected }}
      accessibilityLabel={`${option.label} : pas pour moi`}
    >
      <Animated.View style={[styles.card, selected && styles.cardSelected, { transform: [{ rotate }, { scale: bounce }] }]}>
        <Animated.View
          style={[styles.cardContent, { opacity: state.interpolate({ inputRange: [0, 1], outputRange: [1, 0.4] }) }]}
        >
          <Text style={styles.emoji} accessibilityElementsHidden importantForAccessibility="no">
            {option.emoji}
          </Text>
          <Text style={styles.label} numberOfLines={2}>
            {option.label}
          </Text>
        </Animated.View>
        <Animated.View
          pointerEvents="none"
          style={[
            styles.stampLayer,
            {
              opacity: state,
              transform: [{ scale: state.interpolate({ inputRange: [0, 1], outputRange: [1.6, 1] }) }, { rotate: '-12deg' }],
            },
          ]}
        >
          <Text style={styles.stamp}>Pas pour moi</Text>
        </Animated.View>
      </Animated.View>
    </Pressable>
  );
}

const useStyles = makeStyles((t) => ({
  container: { gap: t.space.md },
  mascotRow: { flexDirection: 'row', alignItems: 'center', gap: t.space.sm },
  bubble: {
    flex: 1,
    backgroundColor: t.colors.surface,
    borderWidth: t.borderWidth.hairline,
    borderColor: t.colors.line,
    borderRadius: t.radius.lg,
    paddingVertical: t.space.xs,
    paddingHorizontal: t.space.sm,
  },
  bubbleText: { ...t.type.callout, color: t.colors.ink },
  spicyRow: { flexDirection: 'row', alignItems: 'center', gap: t.space.md },
  spicyText: { flex: 1, gap: t.space.xxs },
  spicyLabel: { ...t.type.bodyBold, color: t.colors.ink },
  hint: { ...t.type.caption, color: t.colors.ink3 },
  count: { ...t.type.overline, color: t.colors.ink3 },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: t.space.xs },
  // Trois colonnes : (100 % − 2 écarts) / 3.
  cell: { width: '31.5%', minHeight: t.layout.minTouch },
  card: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: t.colors.surface,
    borderWidth: t.borderWidth.hairline,
    borderColor: t.colors.line,
    borderRadius: t.radius.md,
    paddingVertical: t.space.sm,
    paddingHorizontal: t.space.xxs,
  },
  // Sélection : bord plus épais (règle de la charte), jamais la couleur seule.
  cardSelected: { borderWidth: t.borderWidth.selected, borderColor: t.colors.border },
  cardContent: { alignItems: 'center', gap: t.space.xxs },
  emoji: { fontSize: 32, lineHeight: 40 },
  label: { ...t.type.caption, color: t.colors.ink, textAlign: 'center' },
  // Tampon posé au centre de la carte, par-dessus son contenu.
  stampLayer: { position: 'absolute', top: 0, right: 0, bottom: 0, left: 0, alignItems: 'center', justifyContent: 'center' },
  stamp: {
    ...t.type.overline,
    color: t.colors.ink,
    backgroundColor: t.colors.surface,
    borderWidth: t.borderWidth.selected,
    borderColor: t.colors.ink,
    borderRadius: t.radius.sm,
    paddingVertical: t.space.xxs,
    paddingHorizontal: t.space.xs,
    overflow: 'hidden',
  },
}));

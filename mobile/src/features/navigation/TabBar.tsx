import type { Tabs } from 'expo-router';
import type { LucideIcon } from 'lucide-react-native';
import type { ComponentProps } from 'react';
import { Animated, Pressable, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { ChefHat, House, Plus, Refrigerator, User } from '@/components/icons/lucide';
import { useStickerPress } from '@/components/useStickerPress';
import { makeStyles, useTheme } from '@/theme';

// Propriétés reçues par une barre d'onglets personnalisée d'Expo Router.
type TabBarProps = Parameters<NonNullable<ComponentProps<typeof Tabs>['tabBar']>>[0];

const TAB_ICONS: Record<string, LucideIcon> = {
  index: Refrigerator,
  recipes: ChefHat,
  household: House,
  profile: User,
};

/**
 * Barre d'onglets de la charte : flottante (coins arrondis, trait fin, ombre douce), en
 * 5 emplacements : deux onglets, le bouton « + » central surélevé, deux onglets.
 * Onglet actif : pastille derrière l'icône et libellé en gras (jamais la couleur seule).
 */
export function TabBar({ state, descriptors, navigation, onAddPress }: TabBarProps & { onAddPress: () => void }) {
  const theme = useTheme();
  const styles = useStyles();
  const insets = useSafeAreaInsets();

  const tabs = state.routes.map((route, index) => {
    const focused = state.index === index;
    const label = descriptors[route.key]?.options.title ?? route.name;
    const Icon = TAB_ICONS[route.name] ?? User;

    // Comportement standard d'un onglet (voir la documentation de React Navigation).
    const onPress = () => {
      const event = navigation.emit({ type: 'tabPress', target: route.key, canPreventDefault: true });
      if (!focused && !event.defaultPrevented) {
        navigation.navigate(route.name, route.params);
      }
    };

    return (
      <Pressable
        key={route.key}
        onPress={onPress}
        accessibilityRole="tab"
        accessibilityState={{ selected: focused }}
        accessibilityLabel={label}
        style={styles.tab}
      >
        <View style={[styles.iconPill, focused && styles.iconPillActive]}>
          <Icon size={24} strokeWidth={2} color={focused ? theme.colors.ink : theme.colors.ink3} />
        </View>
        <Text
          style={[styles.label, focused ? styles.labelActive : null]}
          numberOfLines={1}
          // Texte agrandi respecté, dans une limite qui garde la barre lisible.
          maxFontSizeMultiplier={1.3}
        >
          {label}
        </Text>
      </Pressable>
    );
  });

  return (
    <View style={[styles.container, { paddingBottom: Math.max(insets.bottom, theme.layout.tabBarInset) }]}>
      <View style={styles.bar} accessibilityRole="tablist">
        {tabs.slice(0, 2)}
        <View style={styles.addSlot}>
          <AddButton onPress={onAddPress} />
        </View>
        {tabs.slice(2)}
      </View>
    </View>
  );
}

/** Bouton « + » central : rond, surélevé, avec l'ombre sticker des boutons d'action. */
function AddButton({ onPress }: { onPress: () => void }) {
  const theme = useTheme();
  const styles = useStyles();
  const { onPressIn, onPressOut, translateY } = useStickerPress();

  return (
    <Pressable
      onPress={onPress}
      onPressIn={onPressIn}
      onPressOut={onPressOut}
      accessibilityRole="button"
      accessibilityLabel="Ajouter"
      accessibilityHint="Scanner un produit, saisir à la main, proposer une recette ou scanner un ticket"
      style={styles.addWrapper}
    >
      <View style={styles.addShadow} />
      <Animated.View style={[styles.addFace, { transform: [{ translateY }] }]}>
        <Plus size={30} strokeWidth={2.5} color={theme.colors.onPrimary} />
      </Animated.View>
    </Pressable>
  );
}

const useStyles = makeStyles((t) => ({
  container: { backgroundColor: t.colors.bg, paddingHorizontal: t.layout.tabBarInset },
  bar: {
    flexDirection: 'row',
    alignItems: 'center',
    height: t.layout.tabBarHeight,
    backgroundColor: t.colors.surface,
    borderWidth: t.borderWidth.hairline,
    borderColor: t.colors.line,
    borderRadius: t.radius.xl,
    // Ombre douce de la barre (tokens surfaces.tabBar) ; Android : élévation équivalente.
    shadowColor: t.colors.shadow,
    shadowOpacity: t.scheme === 'dark' ? 0.4 : 0.1,
    shadowRadius: 24,
    shadowOffset: { width: 0, height: 8 },
    elevation: 6,
  },
  tab: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: 2, minHeight: t.layout.minTouch },
  iconPill: { width: 52, height: 30, borderRadius: t.radius.pill, alignItems: 'center', justifyContent: 'center' },
  iconPillActive: { backgroundColor: t.colors.primarySoft },
  label: { ...t.type.caption, color: t.colors.ink3 },
  labelActive: { fontFamily: t.fonts.bodyHeavy, color: t.colors.ink },
  addSlot: { width: t.layout.fabSize + t.space.md, alignItems: 'center', alignSelf: 'flex-start' },
  // Le « + » dépasse de la barre vers le haut (fabLift) ; la place de son ombre est réservée dessous.
  addWrapper: { marginTop: -t.layout.fabLift, paddingBottom: t.shadow.button.offsetY },
  addShadow: {
    position: 'absolute',
    top: t.shadow.button.offsetY,
    left: 0,
    width: t.layout.fabSize,
    height: t.layout.fabSize,
    borderRadius: t.layout.fabSize / 2,
    backgroundColor: t.colors.shadow,
  },
  addFace: {
    width: t.layout.fabSize,
    height: t.layout.fabSize,
    borderRadius: t.layout.fabSize / 2,
    borderWidth: t.borderWidth.fab,
    borderColor: t.colors.buttonBorder,
    backgroundColor: t.colors.primary,
    alignItems: 'center',
    justifyContent: 'center',
  },
}));

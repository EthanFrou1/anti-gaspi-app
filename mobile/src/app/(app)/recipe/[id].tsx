import { router, useLocalSearchParams } from 'expo-router';
import { useEffect, useState } from 'react';
import { ActivityIndicator, Alert, Pressable, Share, Text, View } from 'react-native';
import { api } from '@/api/client';
import { asApiError } from '@/api/errors';
import type { InventoryItem, Recipe } from '@/api/types';
import { useAuth } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { CheckBox } from '@/components/CheckBox';
import { ErrorBanner } from '@/components/ErrorBanner';
import { Clock, Refrigerator, Share2, Star } from '@/components/icons/lucide';
import { Screen } from '@/components/Screen';
import { WarningNote } from '@/components/WarningNote';
import { allergyWarning, CONSTRAINT_EXCLUSION_NOTE, decodeRestrictions } from '@/features/recipes/meal';
import { APP_NAME } from '@/config';
import { canModifyItem } from '@/features/inventory/rules';
import { refreshExpiryReminders } from '@/features/notifications/reminders';
import {
  excludedProductsLabel,
  favoriteLabel,
  formatPrepTime,
  formatRecipeForSharing,
  fridgeIngredients,
} from '@/features/recipes/rules';
import { makeStyles, useTheme } from '@/theme';

export default function RecipeScreen() {
  const theme = useTheme();
  const styles = useStyles();
  // restrictions : contraintes d'allergène du repas, passées par la navigation juste après la
  // génération (jamais enregistrées : la recette rouverte plus tard n'en a pas).
  const { id, restrictions, constraintsNote } = useLocalSearchParams<{
    id: string;
    restrictions?: string;
    constraintsNote?: string;
  }>();
  const reinforcedWarning = allergyWarning(decodeRestrictions(restrictions));
  const { state } = useAuth();
  const user = state.status === 'signedIn' ? state.user : null;
  const householdId = user?.householdId ?? null;

  const [recipe, setRecipe] = useState<Recipe | null>(null);
  // Produits du frigo encore actifs, pour les cases de « J'ai cuisiné ».
  const [activeItems, setActiveItems] = useState<Map<string, InventoryItem>>(new Map());
  const [error, setError] = useState<string | null>(null);
  const [cooking, setCooking] = useState(false);
  const [checked, setChecked] = useState<string[]>([]);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!householdId || !id) return;
    Promise.all([api.recipes.get(householdId, id), api.inventory.list(householdId)])
      .then(([r, items]) => {
        setRecipe(r);
        setActiveItems(new Map(items.map((i) => [i.id, i])));
      })
      .catch((e: unknown) => setError(asApiError(e).message));
  }, [householdId, id]);

  if (!user || !householdId) return null;

  if (!recipe) {
    return (
      <Screen hasHeader>
        {error ? <ErrorBanner message={error} /> : <ActivityIndicator style={styles.loader} size="large" color={theme.colors.primary} />}
      </Screen>
    );
  }

  // Cases proposées : produits de la recette encore au frigo, et que j'ai le droit de modifier.
  const candidates = fridgeIngredients(recipe)
    .map((ingredient) => ({ ingredient, item: activeItems.get(ingredient.inventoryItemId) }))
    .filter((c): c is { ingredient: (typeof c)['ingredient']; item: InventoryItem } => c.item !== undefined);

  async function toggleFavorite() {
    setError(null);
    try {
      const updated = recipe!.isFavorite
        ? await api.recipes.removeFavorite(householdId!, recipe!.id)
        : await api.recipes.addFavorite(householdId!, recipe!.id);
      setRecipe(updated);
    } catch (e) {
      setError(asApiError(e).message);
    }
  }

  function share() {
    // Feuille de partage native (SMS, messagerie…) : la recette en texte simple.
    void Share.share({ message: formatRecipeForSharing(recipe!, APP_NAME) });
  }

  function startCooking() {
    // Par défaut, tout ce qui vient du frigo (et que je peux modifier) est coché.
    setChecked(candidates.filter((c) => canModifyItem(c.item, user!.id)).map((c) => c.item.id));
    setCooking(true);
  }

  function toggle(itemId: string) {
    setChecked((current) => (current.includes(itemId) ? current.filter((i) => i !== itemId) : [...current, itemId]));
  }

  async function confirmCooked() {
    setSaving(true);
    setError(null);
    try {
      const { consumedCount } = await api.recipes.markCooked(householdId!, recipe!.id, checked);
      // Les produits retirés ne doivent plus apparaître dans les rappels.
      if (consumedCount > 0) void refreshExpiryReminders(householdId, user!.id);
      Alert.alert(
        'Bon appétit !',
        consumedCount > 0 ? `${consumedCount} produit(s) retiré(s) du frigo. Autant de gaspillage évité !` : 'Rien n\'a été retiré du frigo.',
      );
      router.back();
    } catch (e) {
      setError(asApiError(e).message);
      setSaving(false);
    }
  }

  return (
    <Screen hasHeader>
      <Text style={styles.title}>{recipe.title}</Text>
      <View style={styles.metaRow}>
        <Clock size={18} strokeWidth={2} color={theme.colors.ink2} />
        <Text style={styles.meta}>
          {formatPrepTime(recipe.prepMinutes)} · {recipe.servings} portion{recipe.servings > 1 ? 's' : ''}
        </Text>
      </View>

      <View style={styles.actions}>
        <Pressable
          onPress={() => void toggleFavorite()}
          style={[styles.actionButton, recipe.isFavorite && styles.actionButtonActive]}
          accessibilityRole="button"
          accessibilityState={{ selected: recipe.isFavorite }}
        >
          {/* Favori : étoile pleine ET bord plus épais (jamais la couleur seule). */}
          <Star
            size={18}
            strokeWidth={2}
            color={recipe.isFavorite ? theme.palette.citron.text : theme.colors.ink2}
            fill={recipe.isFavorite ? theme.palette.citron.base : 'transparent'}
          />
          <Text style={styles.actionText}>{favoriteLabel(recipe)}</Text>
        </Pressable>
        <Pressable onPress={share} style={styles.actionButton} accessibilityRole="button">
          <Share2 size={18} strokeWidth={2} color={theme.colors.ink2} />
          <Text style={styles.actionText}>Partager</Text>
        </Pressable>
      </View>

      {reinforcedWarning ? (
        <WarningNote strong>{reinforcedWarning}</WarningNote>
      ) : (
        <WarningNote>Recette proposée par une IA : vérifie toujours les étiquettes, en particulier en cas d'allergie.</WarningNote>
      )}

      <Text style={styles.heading}>Ingrédients</Text>
      {recipe.ingredients.map((ingredient, index) => (
        <View key={index} style={styles.ingredientRow}>
          {ingredient.inventoryItemId ? (
            <Refrigerator size={18} strokeWidth={2} color={theme.colors.primaryText} />
          ) : (
            <View style={styles.bullet} />
          )}
          <Text style={[styles.ingredient, ingredient.inventoryItemId ? styles.fromFridge : null]}>
            {ingredient.name}
            {ingredient.quantity ? ` : ${ingredient.quantity}` : ''}
          </Text>
        </View>
      ))}
      <View style={styles.legend}>
        <Refrigerator size={14} strokeWidth={2} color={theme.colors.primaryText} />
        <Text style={styles.hint}>produit de ton frigo, à utiliser en priorité</Text>
      </View>
      {excludedProductsLabel(recipe.excludedByPreferences) ? (
        <Text style={styles.hint}>{excludedProductsLabel(recipe.excludedByPreferences)}</Text>
      ) : null}
      {constraintsNote === '1' ? <Text style={styles.hint}>{CONSTRAINT_EXCLUSION_NOTE}</Text> : null}

      <Text style={styles.heading}>Préparation</Text>
      {recipe.steps.map((step, index) => (
        <View key={index} style={styles.step}>
          <View style={styles.stepNumber}>
            <Text style={styles.stepNumberText}>{index + 1}</Text>
          </View>
          <Text style={styles.stepText}>{step}</Text>
        </View>
      ))}

      <ErrorBanner message={error ?? undefined} />

      {!cooking ? (
        <Button title="J'ai cuisiné cette recette" onPress={startCooking} />
      ) : (
        <View style={styles.cooked}>
          <Text style={styles.heading}>Quels produits as-tu terminés ?</Text>
          {candidates.length === 0 ? (
            <Text style={styles.hint}>Aucun produit de cette recette n'est encore dans le frigo.</Text>
          ) : null}
          {candidates.map(({ item }) => {
            const allowed = canModifyItem(item, user.id);
            const isChecked = checked.includes(item.id);
            return (
              <Pressable
                key={item.id}
                onPress={() => allowed && toggle(item.id)}
                disabled={!allowed}
                accessibilityRole="checkbox"
                accessibilityState={{ checked: isChecked, disabled: !allowed }}
                style={styles.checkRow}
              >
                <CheckBox checked={isChecked} disabled={!allowed} />
                <Text style={[styles.checkLabel, !allowed && styles.disabled]}>
                  {item.name}
                  {!allowed ? ` (perso de ${item.ownerDisplayName ?? 'un autre membre'})` : ''}
                </Text>
              </Pressable>
            );
          })}
          <Button
            title={checked.length > 0 ? `Retirer ${checked.length} produit(s) du frigo` : 'Valider sans rien retirer'}
            onPress={() => void confirmCooked()}
            loading={saving}
          />
          <Button title="Annuler" variant="secondary" onPress={() => setCooking(false)} disabled={saving} />
        </View>
      )}
    </Screen>
  );
}

const useStyles = makeStyles((t) => ({
  loader: { marginTop: t.space['2xl'] },
  title: { ...t.type.title2, color: t.colors.ink },
  metaRow: { flexDirection: 'row', alignItems: 'center', gap: t.space.xxs },
  meta: { ...t.type.callout, color: t.colors.ink2 },
  actions: { flexDirection: 'row', flexWrap: 'wrap', gap: t.space.xs },
  actionButton: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: t.space.xxs,
    minHeight: t.layout.minTouch,
    borderWidth: t.borderWidth.hairline,
    borderColor: t.colors.line,
    backgroundColor: t.colors.surface,
    borderRadius: t.radius.pill,
    paddingHorizontal: t.space.md,
  },
  actionButtonActive: { borderWidth: t.borderWidth.selected, borderColor: t.colors.border, backgroundColor: t.colors.primarySoft },
  actionText: { ...t.type.callout, color: t.colors.ink },
  heading: { ...t.type.title3, color: t.colors.ink, marginTop: t.space.md },
  ingredientRow: { flexDirection: 'row', alignItems: 'center', gap: t.space.xs },
  bullet: { width: 6, height: 6, borderRadius: 3, marginHorizontal: 6, backgroundColor: t.colors.ink3 },
  ingredient: { ...t.type.body, flex: 1, color: t.colors.ink },
  fromFridge: { fontFamily: t.fonts.bodyBold },
  legend: { flexDirection: 'row', alignItems: 'center', gap: t.space.xxs },
  hint: { ...t.type.caption, color: t.colors.ink3 },
  step: { flexDirection: 'row', gap: t.space.sm, alignItems: 'flex-start' },
  stepNumber: {
    width: 28,
    height: 28,
    borderRadius: 14,
    backgroundColor: t.colors.primarySoft,
    alignItems: 'center',
    justifyContent: 'center',
  },
  stepNumberText: { ...t.type.callout, fontFamily: t.fonts.heading, color: t.colors.primaryText },
  stepText: { ...t.type.body, flex: 1, color: t.colors.ink },
  cooked: { gap: t.space.sm },
  checkRow: { flexDirection: 'row', gap: t.space.sm, alignItems: 'center', minHeight: t.layout.minTouch },
  checkLabel: { ...t.type.body, flex: 1, color: t.colors.ink },
  disabled: { color: t.colors.ink3 },
}));

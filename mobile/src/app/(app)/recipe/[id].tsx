import { router, useLocalSearchParams } from 'expo-router';
import { useEffect, useState } from 'react';
import { ActivityIndicator, Alert, Pressable, Share, StyleSheet, Text, View } from 'react-native';
import { api } from '@/api/client';
import { asApiError } from '@/api/errors';
import type { InventoryItem, Recipe } from '@/api/types';
import { useAuth } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { ErrorBanner } from '@/components/ErrorBanner';
import { Screen } from '@/components/Screen';
import { APP_NAME } from '@/config';
import { canModifyItem } from '@/features/inventory/rules';
import { favoriteLabel, formatPrepTime, formatRecipeForSharing, fridgeIngredients } from '@/features/recipes/rules';
import { colors, spacing } from '@/theme';

export default function RecipeScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
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
        {error ? <ErrorBanner message={error} /> : <ActivityIndicator style={styles.loader} size="large" color={colors.primary} />}
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
      <Text style={styles.meta}>
        ⏱ {formatPrepTime(recipe.prepMinutes)} · {recipe.servings} portion{recipe.servings > 1 ? 's' : ''}
      </Text>

      <View style={styles.actions}>
        <Pressable
          onPress={() => void toggleFavorite()}
          style={[styles.actionButton, recipe.isFavorite && styles.actionButtonActive]}
          accessibilityRole="button"
          accessibilityState={{ selected: recipe.isFavorite }}
        >
          <Text style={[styles.actionText, recipe.isFavorite && styles.actionTextActive]}>
            {recipe.isFavorite ? '★' : '☆'} {favoriteLabel(recipe)}
          </Text>
        </Pressable>
        <Pressable onPress={share} style={styles.actionButton} accessibilityRole="button">
          <Text style={styles.actionText}>Partager</Text>
        </Pressable>
      </View>

      <Text style={styles.warning}>
        ⚠ Recette proposée par une IA : vérifie toujours les étiquettes, en particulier en cas d'allergie.
      </Text>

      <Text style={styles.heading}>Ingrédients</Text>
      {recipe.ingredients.map((ingredient, index) => (
        <Text key={index} style={[styles.ingredient, ingredient.inventoryItemId ? styles.fromFridge : null]}>
          {ingredient.inventoryItemId ? '🧊 ' : '• '}
          {ingredient.name}
          {ingredient.quantity ? ` : ${ingredient.quantity}` : ''}
        </Text>
      ))}
      <Text style={styles.hint}>🧊 = produit de ton frigo</Text>

      <Text style={styles.heading}>Préparation</Text>
      {recipe.steps.map((step, index) => (
        <View key={index} style={styles.step}>
          <Text style={styles.stepNumber}>{index + 1}</Text>
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
                <Text style={styles.checkbox}>{isChecked ? '☑' : '☐'}</Text>
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

const styles = StyleSheet.create({
  loader: { marginTop: spacing.xl },
  title: { fontSize: 24, fontWeight: '700', color: colors.text },
  meta: { fontSize: 15, color: colors.mutedText },
  actions: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  actionButton: {
    borderWidth: 1,
    borderColor: colors.primary,
    borderRadius: 16,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.xs + 2,
  },
  actionButtonActive: { backgroundColor: colors.primary },
  actionText: { fontSize: 14, color: colors.primary, fontWeight: '600' },
  actionTextActive: { color: colors.primaryText },
  warning: { backgroundColor: '#FFF8E1', color: colors.text, padding: spacing.md, borderRadius: 8, fontSize: 13 },
  heading: { fontSize: 18, fontWeight: '700', color: colors.text, marginTop: spacing.md },
  ingredient: { fontSize: 15, color: colors.text },
  fromFridge: { fontWeight: '600' },
  hint: { fontSize: 12, color: colors.mutedText },
  step: { flexDirection: 'row', gap: spacing.sm },
  stepNumber: { fontSize: 15, fontWeight: '700', color: colors.primary, minWidth: 20 },
  stepText: { flex: 1, fontSize: 15, color: colors.text },
  cooked: { gap: spacing.sm },
  checkRow: { flexDirection: 'row', gap: spacing.sm, alignItems: 'center', paddingVertical: spacing.xs },
  checkbox: { fontSize: 22, color: colors.primary },
  checkLabel: { flex: 1, fontSize: 15, color: colors.text },
  disabled: { color: colors.mutedText },
});

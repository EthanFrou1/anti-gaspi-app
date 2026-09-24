import { router, useLocalSearchParams } from 'expo-router';
import { useEffect, useState } from 'react';
import { ActivityIndicator, Alert, Pressable, Text, View } from 'react-native';
import { api } from '@/api/client';
import { asApiError, type ApiError } from '@/api/errors';
import type { InventoryItem, SaveInventoryItemRequest } from '@/api/types';
import { useAuth } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { ErrorBanner } from '@/components/ErrorBanner';
import { Screen } from '@/components/Screen';
import { ItemForm } from '@/features/inventory/ItemForm';
import { canModifyItem } from '@/features/inventory/rules';
import { useCategories } from '@/features/inventory/useCategories';
import { makeStyles, useTheme } from '@/theme';

export default function EditItemScreen() {
  const theme = useTheme();
  const styles = useStyles();
  const { id } = useLocalSearchParams<{ id: string }>();
  const { state } = useAuth();
  const user = state.status === 'signedIn' ? state.user : null;
  const householdId = user?.householdId ?? null;
  const { categories, error: categoriesError } = useCategories();

  const [item, setItem] = useState<InventoryItem | null>(null);
  const [loadError, setLoadError] = useState<ApiError | null>(null);
  const [saveError, setSaveError] = useState<ApiError | null>(null);

  useEffect(() => {
    if (!householdId || !id) return;
    api.inventory
      .get(householdId, id)
      .then(setItem)
      .catch((e: unknown) => setLoadError(asApiError(e)));
  }, [householdId, id]);

  if (!user || !householdId) {
    return null;
  }

  const error = loadError ?? categoriesError;
  if (error) {
    return (
      <Screen hasHeader>
        <ErrorBanner message={error.message} />
        <Button title="Retour" variant="secondary" onPress={() => router.back()} />
      </Screen>
    );
  }

  if (!item || !categories) {
    return (
      <Screen hasHeader>
        <ActivityIndicator style={styles.loader} size="large" color={theme.colors.primary} />
      </Screen>
    );
  }

  const editable = canModifyItem(item, user.id);

  async function handleSubmit(request: SaveInventoryItemRequest) {
    setSaveError(null);
    try {
      await api.inventory.update(householdId!, item!.id, request);
      router.back();
    } catch (e) {
      setSaveError(asApiError(e));
    }
  }

  function confirmDelete() {
    Alert.alert(
      'Supprimer ce produit ?',
      'À utiliser seulement pour une erreur de saisie. S\'il a été mangé ou jeté, utilise plutôt ces boutons : ' +
        'ça alimentera ton compteur anti-gaspi.',
      [
        { text: 'Annuler', style: 'cancel' },
        { text: 'Supprimer', style: 'destructive', onPress: () => void runAction(() => api.inventory.delete(householdId!, item!.id)) },
      ],
    );
  }

  async function runAction(action: () => Promise<unknown>) {
    try {
      await action();
      router.back();
    } catch (e) {
      Alert.alert('Action impossible', asApiError(e).message);
    }
  }

  return (
    <Screen hasHeader>
      {!editable ? (
        <Text style={styles.readOnly}>
          Produit perso de {item.ownerDisplayName ?? 'un autre membre'} : seul son propriétaire peut le modifier.
        </Text>
      ) : null}

      <ItemForm
        categories={categories}
        initial={{
          name: item.name,
          categoryId: item.categoryId,
          quantity: item.quantity,
          unit: item.unit,
          purchasedOn: item.purchasedOn,
          barcode: item.barcode,
          // Date estimée : on la laisse suivre la catégorie (null) ; date saisie : on la garde.
          manualExpiresOn: item.expiryIsEstimated ? null : item.expiresOn,
          isPersonal: item.ownerUserId !== null,
        }}
        submitLabel="Enregistrer"
        onSubmit={handleSubmit}
        error={saveError}
        readOnly={!editable}
      />

      {editable ? (
        <View style={styles.actions}>
          <Button title="Consommé" variant="secondary" onPress={() => void runAction(() => api.inventory.consume(householdId, item.id))} />
          <Button title="Jeté" variant="secondary" onPress={() => void runAction(() => api.inventory.discard(householdId, item.id))} />
          <Pressable onPress={confirmDelete} accessibilityRole="button" style={styles.deleteButton} hitSlop={6}>
            <Text style={styles.deleteText}>Supprimer (erreur de saisie)</Text>
          </Pressable>
        </View>
      ) : null}
    </Screen>
  );
}

const useStyles = makeStyles((t) => ({
  loader: { marginTop: t.space['2xl'] },
  // Bandeau neutre (mêmes couleurs que le badge « Perso ») : information, pas une erreur.
  readOnly: {
    ...t.type.callout,
    backgroundColor: t.persoBadge.bg,
    color: t.persoBadge.fg,
    padding: t.space.md,
    borderRadius: t.radius.sm,
  },
  actions: {
    gap: t.space.sm,
    marginTop: t.space.md,
    paddingTop: t.space.md,
    borderTopWidth: t.borderWidth.hairline,
    borderTopColor: t.colors.line,
  },
  deleteButton: { minHeight: t.layout.minTouch, alignItems: 'center', justifyContent: 'center' },
  deleteText: { ...t.type.callout, color: t.colors.danger, textDecorationLine: 'underline' },
}));

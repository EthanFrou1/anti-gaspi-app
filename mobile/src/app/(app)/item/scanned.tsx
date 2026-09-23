import { router, useLocalSearchParams } from 'expo-router';
import { useEffect, useState } from 'react';
import { ActivityIndicator, Image, StyleSheet, Text, View } from 'react-native';
import { api } from '@/api/client';
import { asApiError, type ApiError } from '@/api/errors';
import type { ProductSuggestion, SaveInventoryItemRequest } from '@/api/types';
import { useAuth } from '@/auth/AuthContext';
import { ErrorBanner } from '@/components/ErrorBanner';
import { Screen } from '@/components/Screen';
import { ItemForm, type ItemFormInitial } from '@/features/inventory/ItemForm';
import { useCategories } from '@/features/inventory/useCategories';
import { askReminderPermission } from '@/features/notifications/reminders';
import { colors, spacing } from '@/theme';

type Lookup =
  | { status: 'loading' }
  | { status: 'found'; suggestion: ProductSuggestion }
  | { status: 'notFound' }
  | { status: 'unavailable'; message: string };

/**
 * Confirmation obligatoire après un scan : l'utilisateur vérifie et corrige la fiche
 * avant l'ajout. C'est le SEUL écran qui affiche l'image Open Food Facts (règle du projet).
 * Produit inconnu ou recherche indisponible : on bascule en saisie manuelle, code-barres conservé.
 */
export default function ScannedItemScreen() {
  const { barcode } = useLocalSearchParams<{ barcode: string }>();
  const { state } = useAuth();
  const householdId = state.status === 'signedIn' ? state.user.householdId : null;
  const userId = state.status === 'signedIn' ? state.user.id : null;
  const { categories, error: categoriesError } = useCategories();
  const [lookup, setLookup] = useState<Lookup>({ status: 'loading' });
  const [saveError, setSaveError] = useState<ApiError | null>(null);

  useEffect(() => {
    if (!barcode) return;
    let active = true;
    api.products
      .lookupBarcode(barcode)
      .then((suggestion) => {
        if (active) setLookup(suggestion ? { status: 'found', suggestion } : { status: 'notFound' });
      })
      .catch((e: unknown) => {
        if (active) setLookup({ status: 'unavailable', message: asApiError(e).message });
      });
    return () => {
      active = false;
    };
  }, [barcode]);

  async function handleSubmit(request: SaveInventoryItemRequest) {
    if (!householdId) return;
    setSaveError(null);
    try {
      await api.inventory.create(householdId, request);
      router.back();
      // Premier produit ajouté : c'est le bon moment pour proposer les rappels.
      if (userId) void askReminderPermission(householdId, userId);
    } catch (e) {
      setSaveError(asApiError(e));
    }
  }

  if (lookup.status === 'loading' || (!categories && !categoriesError)) {
    return (
      <Screen hasHeader>
        <ActivityIndicator style={styles.loader} size="large" color={colors.primary} />
        <Text style={styles.centeredText}>Recherche du produit…</Text>
      </Screen>
    );
  }

  const suggestion = lookup.status === 'found' ? lookup.suggestion : null;
  const initial: ItemFormInitial = {
    barcode: barcode ?? null,
    name: suggestion?.name ?? undefined,
    categoryId: suggestion?.categoryId ?? undefined,
    quantity: suggestion?.quantity ?? undefined,
    unit: suggestion?.unit ?? undefined,
  };

  return (
    <Screen hasHeader>
      {categoriesError ? <ErrorBanner message={categoriesError.message} /> : null}

      {suggestion ? (
        <View style={styles.preview}>
          {suggestion.imageUrl ? (
            <Image
              source={{ uri: suggestion.imageUrl }}
              style={styles.image}
              resizeMode="contain"
              accessibilityLabel={`Photo du produit ${suggestion.name ?? ''}`}
            />
          ) : null}
          <View style={styles.previewText}>
            <Text style={styles.productName}>{suggestion.name ?? 'Produit sans nom'}</Text>
            {suggestion.brand ? <Text style={styles.brand}>{suggestion.brand}</Text> : null}
            {/* Licence ODbL : la source doit être indiquée avec les données. */}
            <Text style={styles.source}>
              Données {suggestion.source}, à vérifier avant d'ajouter.
            </Text>
          </View>
        </View>
      ) : (
        <Text style={styles.info}>
          {lookup.status === 'notFound'
            ? 'Ce produit est inconnu d\'Open Food Facts : complète sa fiche ci-dessous.'
            : `${lookup.status === 'unavailable' ? lookup.message : ''}`}
        </Text>
      )}

      {categories ? (
        <ItemForm
          categories={categories}
          initial={initial}
          submitLabel="Ajouter au frigo"
          onSubmit={handleSubmit}
          error={saveError}
        />
      ) : null}
    </Screen>
  );
}

const styles = StyleSheet.create({
  loader: { marginTop: spacing.xl },
  centeredText: { textAlign: 'center', color: colors.mutedText },
  preview: {
    flexDirection: 'row',
    gap: spacing.md,
    alignItems: 'center',
    padding: spacing.md,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: 8,
  },
  image: { width: 80, height: 80 },
  previewText: { flex: 1, gap: spacing.xs },
  productName: { fontSize: 18, fontWeight: '700', color: colors.text },
  brand: { fontSize: 14, color: colors.mutedText },
  source: { fontSize: 12, color: colors.mutedText, fontStyle: 'italic' },
  info: {
    backgroundColor: '#FFF8E1',
    color: colors.text,
    padding: spacing.md,
    borderRadius: 8,
  },
});

import { router } from 'expo-router';
import { useState } from 'react';
import { ActivityIndicator, StyleSheet } from 'react-native';
import { api } from '@/api/client';
import { asApiError, type ApiError } from '@/api/errors';
import type { SaveInventoryItemRequest } from '@/api/types';
import { useAuth } from '@/auth/AuthContext';
import { ErrorBanner } from '@/components/ErrorBanner';
import { Screen } from '@/components/Screen';
import { ItemForm } from '@/features/inventory/ItemForm';
import { useCategories } from '@/features/inventory/useCategories';
import { colors, spacing } from '@/theme';

export default function NewItemScreen() {
  const { state } = useAuth();
  const householdId = state.status === 'signedIn' ? state.user.householdId : null;
  const { categories, error: categoriesError } = useCategories();
  const [error, setError] = useState<ApiError | null>(null);

  async function handleSubmit(request: SaveInventoryItemRequest) {
    if (!householdId) return;
    setError(null);
    try {
      await api.inventory.create(householdId, request);
      // La liste du frigo se recharge d'elle-même en reprenant le focus.
      router.back();
    } catch (e) {
      setError(asApiError(e));
    }
  }

  return (
    <Screen hasHeader>
      {categoriesError ? <ErrorBanner message={categoriesError.message} /> : null}
      {categories ? (
        <ItemForm categories={categories} submitLabel="Ajouter au frigo" onSubmit={handleSubmit} error={error} />
      ) : categoriesError ? null : (
        <ActivityIndicator style={styles.loader} size="large" color={colors.primary} />
      )}
    </Screen>
  );
}

const styles = StyleSheet.create({
  loader: { marginTop: spacing.xl },
});

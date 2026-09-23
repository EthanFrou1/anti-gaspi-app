import { useFocusEffect } from 'expo-router';
import { useCallback, useState } from 'react';
import { api } from '@/api/client';
import { asApiError, type ApiError } from '@/api/errors';
import type { InventoryItem } from '@/api/types';

/**
 * Produits actifs du foyer. Rechargés à chaque retour sur l'écran (après un ajout
 * ou une modification, ou quand un colocataire a changé l'inventaire entre-temps).
 */
export function useInventory(householdId: string | null) {
  const [items, setItems] = useState<InventoryItem[] | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  const reload = useCallback(async () => {
    if (!householdId) {
      setItems(null);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      setItems(await api.inventory.list(householdId));
    } catch (e) {
      setError(asApiError(e));
    } finally {
      setLoading(false);
    }
  }, [householdId]);

  useFocusEffect(
    useCallback(() => {
      void reload();
    }, [reload]),
  );

  return { items, loading, error, reload };
}

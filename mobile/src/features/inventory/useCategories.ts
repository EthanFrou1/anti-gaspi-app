import { useEffect, useMemo, useState } from 'react';
import { api } from '@/api/client';
import { asApiError, type ApiError } from '@/api/errors';
import type { Category } from '@/api/types';

// Données de référence qui ne changent pas pendant l'utilisation :
// chargées une seule fois, puis gardées en mémoire pour tous les écrans.
let cachedCategories: Category[] | null = null;

export function useCategories() {
  const [categories, setCategories] = useState<Category[] | null>(cachedCategories);
  const [error, setError] = useState<ApiError | null>(null);

  useEffect(() => {
    if (cachedCategories) {
      return;
    }
    let active = true;
    api.categories
      .list()
      .then((list) => {
        cachedCategories = list;
        if (active) setCategories(list);
      })
      .catch((e: unknown) => {
        if (active) setError(asApiError(e));
      });
    // Évite de mettre à jour un écran déjà fermé.
    return () => {
      active = false;
    };
  }, []);

  const byId = useMemo(() => new Map((categories ?? []).map((c) => [c.id, c])), [categories]);

  return { categories, byId, error };
}

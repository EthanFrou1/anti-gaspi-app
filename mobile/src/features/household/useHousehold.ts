import { useCallback, useEffect, useState } from 'react';
import { api } from '@/api/client';
import { asApiError, type ApiError } from '@/api/errors';
import type { Household } from '@/api/types';

type HouseholdState = {
  // undefined = pas encore chargé ; null = l'utilisateur n'a pas de foyer.
  household: Household | null | undefined;
  loading: boolean;
  error: ApiError | null;
  reload: () => Promise<void>;
};

/**
 * Charge le foyer de l'utilisateur connecté et permet de le recharger
 * (après une création, un départ, ou un « tirer pour rafraîchir »).
 */
export function useHousehold(): HouseholdState {
  const [household, setHousehold] = useState<Household | null | undefined>(undefined);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<ApiError | null>(null);

  const reload = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setHousehold(await api.households.getMine());
    } catch (e) {
      setError(asApiError(e));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void reload();
  }, [reload]);

  return { household, loading, error, reload };
}

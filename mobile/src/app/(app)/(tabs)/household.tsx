import { ActivityIndicator } from 'react-native';
import { useAuth } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { ErrorBanner } from '@/components/ErrorBanner';
import { Screen } from '@/components/Screen';
import { HouseholdView } from '@/features/household/HouseholdView';
import { NoHouseholdView } from '@/features/household/NoHouseholdView';
import { useHousehold } from '@/features/household/useHousehold';
import { makeStyles, useTheme } from '@/theme';

/**
 * Accueil : le foyer de l'utilisateur, ou la création / l'entrée dans un foyer.
 * (L'inventaire viendra s'ajouter ici à la fonctionnalité 2.)
 */
export default function HomeScreen() {
  const theme = useTheme();
  const styles = useStyles();
  const { state, refreshUser } = useAuth();
  const { household, loading, error, reload } = useHousehold();

  if (state.status !== 'signedIn') {
    return null;
  }

  // Après création, entrée ou départ : on recharge le foyer ET l'utilisateur
  // (son householdId a changé).
  async function handleChanged() {
    await Promise.all([reload(), refreshUser()]);
  }

  return (
    <Screen hasHeader refreshing={loading && household !== undefined} onRefresh={() => void reload()}>
      {household === undefined && loading ? (
        <ActivityIndicator size="large" color={theme.colors.primary} style={styles.loader} />
      ) : null}

      {error ? (
        <>
          <ErrorBanner message={error.message} />
          <Button title="Réessayer" variant="secondary" onPress={() => void reload()} />
        </>
      ) : null}

      {household === null ? <NoHouseholdView onDone={handleChanged} /> : null}

      {household ? (
        <HouseholdView household={household} myUserId={state.user.id} onChanged={handleChanged} />
      ) : null}
    </Screen>
  );
}

const useStyles = makeStyles((t) => ({
  loader: { marginTop: t.space['2xl'] },
}));

import { router, Tabs } from 'expo-router';
import { useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, Alert, FlatList, Pressable, RefreshControl, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { api } from '@/api/client';
import { asApiError } from '@/api/errors';
import type { InventoryItem } from '@/api/types';
import { useAuth } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { ChoiceChips } from '@/components/ChoiceChips';
import { EmptyState } from '@/components/EmptyState';
import { ErrorBanner } from '@/components/ErrorBanner';
import { Bell } from '@/components/icons/lucide';
import { SavedCelebration } from '@/features/celebration/SavedCelebration';
import { useHousehold } from '@/features/household/useHousehold';
import { InventoryRow } from '@/features/inventory/InventoryRow';
import { canModifyItem, filterItems, type InventoryFilter } from '@/features/inventory/rules';
import { useCategories } from '@/features/inventory/useCategories';
import { useInventory } from '@/features/inventory/useInventory';
import { sendTestReminder, syncExpiryReminders } from '@/features/notifications/reminders';
import { makeStyles, useTheme } from '@/theme';
import { toLocalDateString } from '@/utils/dates';

const FILTERS: { value: InventoryFilter; label: string }[] = [
  { value: 'all', label: 'Tout' },
  { value: 'common', label: 'Commun' },
  { value: 'mine', label: 'À moi' },
];

/**
 * Le frigo du foyer, trié par date de péremption : ce qui périme en premier est en haut.
 */
export default function FridgeScreen() {
  const theme = useTheme();
  const styles = useStyles();
  const { state } = useAuth();
  const user = state.status === 'signedIn' ? state.user : null;
  const householdId = user?.householdId ?? null;

  const { items, loading, error, reload } = useInventory(householdId);
  const { byId: categoriesById } = useCategories();
  // Ordre d'arrivée des membres : chacun garde sa couleur d'avatar sur les produits perso.
  const { household } = useHousehold();
  const memberIds = useMemo(() => household?.members.map((m) => m.userId), [household]);
  const [filter, setFilter] = useState<InventoryFilter>('all');
  // Produit qui vient d'être mangé : célébration « Produit sauvé » (visuelle seulement).
  const [savedProduct, setSavedProduct] = useState<string | null>(null);
  const today = toLocalDateString();

  // Chaque chargement du frigo (retour sur l'écran, produit consommé ou jeté…) reprogramme
  // les rappels de péremption à partir de l'inventaire à jour.
  const userId = user?.id ?? null;
  useEffect(() => {
    if (items && userId) void syncExpiryReminders(items, userId);
  }, [items, userId]);

  const visibleItems = useMemo(
    () => (items && user ? filterItems(items, filter, user.id) : []),
    [items, filter, user],
  );

  if (!user) {
    return null;
  }

  if (!householdId) {
    return (
      <SafeAreaView style={styles.centered} edges={['left', 'right']}>
        <EmptyState
          illustration="fridge"
          title="Ton frigo est partagé au sein d'un foyer"
          text="Crée ton foyer ou rejoins celui de ta coloc pour commencer."
        >
          <Button title="Aller à l'onglet Foyer" onPress={() => router.navigate('/household')} />
        </EmptyState>
      </SafeAreaView>
    );
  }

  function confirmStatus(item: InventoryItem, action: 'consume' | 'discard') {
    const consumed = action === 'consume';
    Alert.alert(
      consumed ? `${item.name} : consommé ?` : `${item.name} : jeté ?`,
      'Il sera retiré du frigo.',
      [
        { text: 'Annuler', style: 'cancel' },
        {
          text: consumed ? 'Consommé' : 'Jeté',
          style: consumed ? 'default' : 'destructive',
          onPress: () => void changeStatus(item, action),
        },
      ],
    );
  }

  async function changeStatus(item: InventoryItem, action: 'consume' | 'discard') {
    try {
      await api.inventory[action](householdId!, item.id);
      if (action === 'consume') setSavedProduct(item.name);
      await reload();
    } catch (e) {
      Alert.alert('Impossible de modifier ce produit', asApiError(e).message);
    }
  }

  // Outil de mise au point (build de développement uniquement) : le vrai prochain résumé dans 10 s.
  async function testReminder() {
    try {
      const result = await sendTestReminder(items ?? [], user!.id);
      const messages = {
        sent: ['Rappel envoyé dans 10 secondes', 'Tu peux verrouiller le téléphone pour le voir arriver.'],
        'nothing-planned': ['Aucun rappel prévu', 'Ajoute un produit qui périme dans les prochains jours.'],
        'not-allowed': ['Notifications non autorisées', "La demande apparaît après l'ajout d'un produit, sinon active-les dans les réglages."],
      } as const;
      const [title, message] = messages[result];
      Alert.alert(title, message);
    } catch (e) {
      Alert.alert('Envoi impossible', e instanceof Error ? e.message : String(e));
    }
  }

  return (
    <SafeAreaView style={styles.container} edges={['left', 'right']}>
      {/* Outil de mise au point dans l'en-tête (build de développement uniquement). */}
      {__DEV__ ? (
        <Tabs.Screen
          options={{
            headerRight: () => (
              <Pressable
                onPress={() => void testReminder()}
                style={styles.devButton}
                hitSlop={8}
                accessibilityRole="button"
                accessibilityLabel="Envoyer le prochain rappel dans 10 secondes (outil de développement)"
              >
                <Bell size={22} strokeWidth={2} color={theme.colors.ink} />
              </Pressable>
            ),
          }}
        />
      ) : null}
      <View style={styles.toolbar}>
        <ChoiceChips options={FILTERS} value={filter} onChange={setFilter} />
      </View>

      {error ? (
        <View style={styles.padded}>
          <ErrorBanner message={error.message} />
        </View>
      ) : null}

      {items === null && loading ? <ActivityIndicator style={styles.loader} size="large" color={theme.colors.primary} /> : null}

      <FlatList
        data={visibleItems}
        keyExtractor={(item) => item.id}
        renderItem={({ item }) => (
          <InventoryRow
            item={item}
            category={categoriesById.get(item.categoryId)}
            today={today}
            canModify={canModifyItem(item, user.id)}
            memberIds={memberIds}
            onPress={() => router.push({ pathname: '/item/[id]', params: { id: item.id } })}
            onConsume={() => confirmStatus(item, 'consume')}
            onDiscard={() => confirmStatus(item, 'discard')}
          />
        )}
        contentContainerStyle={styles.list}
        refreshControl={
          <RefreshControl refreshing={loading && items !== null} onRefresh={() => void reload()} tintColor={theme.colors.primary} />
        }
        ListEmptyComponent={
          items !== null ? (
            <EmptyState
              illustration="emptyFridge"
              title={filter === 'all' ? 'Le frigo est vide' : 'Rien dans cette sélection'}
              text="Ajoute tes courses avec le bouton « + » pour être prévenu avant qu'elles ne périment."
            />
          ) : null
        }
      />

      <SavedCelebration productName={savedProduct} onDone={() => setSavedProduct(null)} />
    </SafeAreaView>
  );
}

const useStyles = makeStyles((t) => ({
  container: { flex: 1, backgroundColor: t.colors.bg },
  toolbar: { paddingHorizontal: t.layout.screenPadding, paddingTop: t.space.xs, paddingBottom: t.space.sm },
  padded: { paddingHorizontal: t.layout.screenPadding, paddingBottom: t.space.sm },
  loader: { marginTop: t.space['2xl'] },
  list: { paddingHorizontal: t.layout.screenPadding, paddingBottom: t.space['2xl'], gap: t.space.sm },
  centered: {
    flex: 1,
    padding: t.layout.screenPadding,
    gap: t.space.md,
    justifyContent: 'center',
    backgroundColor: t.colors.bg,
  },
  devButton: { paddingHorizontal: t.space.md },
}));

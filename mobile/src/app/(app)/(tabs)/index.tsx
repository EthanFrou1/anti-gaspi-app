import { router } from 'expo-router';
import { useMemo, useState } from 'react';
import { ActivityIndicator, Alert, FlatList, RefreshControl, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { api } from '@/api/client';
import { asApiError } from '@/api/errors';
import type { InventoryItem } from '@/api/types';
import { useAuth } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { ChoiceChips } from '@/components/ChoiceChips';
import { ErrorBanner } from '@/components/ErrorBanner';
import { InventoryRow } from '@/features/inventory/InventoryRow';
import { canModifyItem, filterItems, type InventoryFilter } from '@/features/inventory/rules';
import { useCategories } from '@/features/inventory/useCategories';
import { useInventory } from '@/features/inventory/useInventory';
import { colors, spacing } from '@/theme';
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
  const { state } = useAuth();
  const user = state.status === 'signedIn' ? state.user : null;
  const householdId = user?.householdId ?? null;

  const { items, loading, error, reload } = useInventory(householdId);
  const { byId: categoriesById } = useCategories();
  const [filter, setFilter] = useState<InventoryFilter>('all');
  const today = toLocalDateString();

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
        <Text style={styles.emptyTitle}>Ton frigo est partagé au sein d'un foyer</Text>
        <Text style={styles.emptyText}>Crée ton foyer ou rejoins celui de ta coloc pour commencer.</Text>
        <Button title="Aller à l'onglet Foyer" onPress={() => router.navigate('/household')} />
      </SafeAreaView>
    );
  }

  function confirmStatus(item: InventoryItem, action: 'consume' | 'discard') {
    const consumed = action === 'consume';
    Alert.alert(
      consumed ? `${item.name} : consommé ?` : `${item.name} : jeté ?`,
      consumed ? 'Il sera retiré du frigo. Bravo, un produit sauvé !' : 'Il sera retiré du frigo.',
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
      await reload();
    } catch (e) {
      Alert.alert('Impossible de modifier ce produit', asApiError(e).message);
    }
  }

  return (
    <SafeAreaView style={styles.container} edges={['left', 'right']}>
      <View style={styles.toolbar}>
        <ChoiceChips options={FILTERS} value={filter} onChange={setFilter} />
      </View>

      {error ? (
        <View style={styles.padded}>
          <ErrorBanner message={error.message} />
        </View>
      ) : null}

      {items === null && loading ? <ActivityIndicator style={styles.loader} size="large" color={colors.primary} /> : null}

      <FlatList
        data={visibleItems}
        keyExtractor={(item) => item.id}
        renderItem={({ item }) => (
          <InventoryRow
            item={item}
            category={categoriesById.get(item.categoryId)}
            today={today}
            canModify={canModifyItem(item, user.id)}
            onPress={() => router.push({ pathname: '/item/[id]', params: { id: item.id } })}
            onConsume={() => confirmStatus(item, 'consume')}
            onDiscard={() => confirmStatus(item, 'discard')}
          />
        )}
        refreshControl={<RefreshControl refreshing={loading && items !== null} onRefresh={() => void reload()} />}
        ListEmptyComponent={
          items !== null ? (
            <View style={styles.empty}>
              <Text style={styles.emptyTitle}>{filter === 'all' ? 'Le frigo est vide' : 'Rien dans cette sélection'}</Text>
              <Text style={styles.emptyText}>Ajoute tes courses pour être prévenu avant qu'elles ne périment.</Text>
            </View>
          ) : null
        }
      />

      <View style={styles.bottomBar}>
        <View style={styles.bottomButton}>
          <Button title="Scanner" onPress={() => router.push('/item/scan')} />
        </View>
        <View style={styles.bottomButton}>
          <Button title="Saisir à la main" variant="secondary" onPress={() => router.push('/item/new')} />
        </View>
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.background },
  toolbar: { padding: spacing.md, borderBottomWidth: StyleSheet.hairlineWidth, borderBottomColor: colors.border },
  padded: { padding: spacing.md },
  loader: { marginTop: spacing.xl },
  empty: { padding: spacing.xl, gap: spacing.sm, alignItems: 'center' },
  centered: {
    flex: 1,
    padding: spacing.lg,
    gap: spacing.md,
    justifyContent: 'center',
    backgroundColor: colors.background,
  },
  emptyTitle: { fontSize: 18, fontWeight: '700', color: colors.text, textAlign: 'center' },
  emptyText: { fontSize: 15, color: colors.mutedText, textAlign: 'center' },
  bottomBar: {
    flexDirection: 'row',
    gap: spacing.sm,
    padding: spacing.md,
    borderTopWidth: StyleSheet.hairlineWidth,
    borderTopColor: colors.border,
  },
  bottomButton: { flex: 1 },
});

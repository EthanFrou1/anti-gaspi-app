import * as Clipboard from 'expo-clipboard';
import { router, useFocusEffect } from 'expo-router';
import { useCallback, useState } from 'react';
import { ActivityIndicator, Alert, Modal, Pressable, StyleSheet, Text, View } from 'react-native';
import { api } from '@/api/client';
import { asApiError } from '@/api/errors';
import type { GenerateRecipeRequest, Recipe, RecipeQuota } from '@/api/types';
import { useAuth } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { ChoiceChips } from '@/components/ChoiceChips';
import { ErrorBanner } from '@/components/ErrorBanner';
import { MultiChoiceChips } from '@/components/MultiChoiceChips';
import { Screen } from '@/components/Screen';
import { useHousehold } from '@/features/household/useHousehold';
import { formatPrepTime, quotaLabel, toggleDiner } from '@/features/recipes/rules';
import { colors, spacing } from '@/theme';
import { formatShortDate } from '@/utils/dates';

/**
 * Recettes proposées par l'IA avec les produits du frigo, en priorité ceux qui périment
 * en premier. Quota : 3 par jour et par personne.
 */
export default function RecipesScreen() {
  const { state } = useAuth();
  const user = state.status === 'signedIn' ? state.user : null;
  const householdId = user?.householdId ?? null;
  const { household } = useHousehold();

  const [quota, setQuota] = useState<RecipeQuota | null>(null);
  const [history, setHistory] = useState<Recipe[]>([]);
  const [favorites, setFavorites] = useState<Recipe[]>([]);
  const [view, setView] = useState<'recent' | 'favorites'>('recent');
  const [diners, setDiners] = useState<string[]>(user ? [user.id] : []);
  const [generating, setGenerating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!householdId) return;
    try {
      const [q, h, f] = await Promise.all([
        api.recipes.quota(),
        api.recipes.list(householdId),
        api.recipes.favorites(householdId),
      ]);
      setQuota(q);
      setHistory(h);
      setFavorites(f);
    } catch (e) {
      setError(asApiError(e).message);
    }
  }, [householdId]);

  // Recharge en revenant sur l'onglet (quota consommé, recette cuisinée entre-temps…).
  useFocusEffect(
    useCallback(() => {
      void load();
    }, [load]),
  );

  if (!user) {
    return null;
  }

  if (!householdId) {
    return (
      <Screen hasHeader>
        <Text style={styles.title}>Des recettes avec ce que tu as</Text>
        <Text style={styles.text}>Crée ou rejoins un foyer, ajoute des produits au frigo, et l'app te propose quoi cuisiner.</Text>
        <Button title="Aller à l'onglet Foyer" onPress={() => router.navigate('/household')} />
      </Screen>
    );
  }

  const request: GenerateRecipeRequest = { dinerUserIds: diners, servings: null };

  async function generate() {
    setGenerating(true);
    setError(null);
    try {
      const recipe = await api.recipes.generate(householdId!, request);
      router.push({ pathname: '/recipe/[id]', params: { id: recipe.id } });
    } catch (e) {
      setError(asApiError(e).message);
    } finally {
      setGenerating(false);
      void load();
    }
  }

  // Outil de mise au point (build de développement uniquement) : copie le prompt exact.
  async function copyPrompt() {
    try {
      const preview = await api.recipes.previewPrompt(householdId!, request);
      await Clipboard.setStringAsync(preview.clipboardText);
      Alert.alert('Prompt copié', `${preview.clipboardText.length} caractères, modèle ${preview.model}.`);
    } catch (e) {
      Alert.alert('Aperçu indisponible', asApiError(e).message);
    }
  }

  const members = household?.members ?? [];
  const noQuotaLeft = quota !== null && quota.remaining <= 0;

  return (
    <Screen hasHeader onRefresh={() => void load()}>
      <Text style={styles.title}>Qu'est-ce qu'on mange ?</Text>
      <Text style={styles.text}>Une recette avec les produits de ton frigo, en commençant par ceux qui périment bientôt.</Text>

      {members.length > 1 ? (
        <View style={styles.section}>
          <Text style={styles.subtitle}>Qui mange ?</Text>
          <MultiChoiceChips
            options={members.map((m) => ({
              value: m.userId,
              label: m.userId === user.id ? `${m.displayName} (toi)` : m.displayName,
            }))}
            values={diners}
            onToggle={(id) => setDiners((current) => toggleDiner(current, id))}
          />
          <Text style={styles.hint}>Les régimes et allergies de chacun sont respectés, sans être dévoilés.</Text>
        </View>
      ) : null}

      <ErrorBanner message={error ?? undefined} />

      <View style={styles.generateRow}>
        <View style={styles.generateButton}>
          <Button title="Proposer une recette" onPress={() => void generate()} disabled={noQuotaLeft || generating} />
        </View>
        {__DEV__ ? (
          <Pressable
            onPress={() => void copyPrompt()}
            style={styles.devButton}
            accessibilityRole="button"
            accessibilityLabel="Copier le prompt (outil de développement)"
          >
            <Text style={styles.devButtonText}>🤖</Text>
          </Pressable>
        ) : null}
      </View>
      {quota ? <Text style={styles.quota}>{quotaLabel(quota)}</Text> : null}

      <View style={styles.section}>
        <ChoiceChips
          options={[
            { value: 'recent', label: 'Récentes (30 jours)' },
            { value: 'favorites', label: `★ Favoris (${favorites.length})` },
          ]}
          value={view}
          onChange={setView}
        />
        {view === 'favorites' ? (
          <Text style={styles.hint}>Le carnet de recettes du foyer : les favoris de chacun, conservés sans limite de durée.</Text>
        ) : null}
        {(view === 'recent' ? history : favorites).length === 0 ? (
          <Text style={styles.hint}>
            {view === 'recent' ? 'Aucune recette ces 30 derniers jours.' : 'Aucun favori pour l\'instant : mets une étoile sur une recette pour la garder.'}
          </Text>
        ) : null}
        {(view === 'recent' ? history : favorites).map((recipe) => (
          <Pressable
            key={recipe.id}
            onPress={() => router.push({ pathname: '/recipe/[id]', params: { id: recipe.id } })}
            style={styles.historyRow}
            accessibilityRole="button"
          >
            <Text style={styles.historyTitle}>
              {recipe.favoriteCount > 0 ? '★ ' : ''}
              {recipe.title}
            </Text>
            <Text style={styles.historyMeta}>
              {formatPrepTime(recipe.prepMinutes)} · {formatShortDate(recipe.createdAt.slice(0, 10))}
            </Text>
          </Pressable>
        ))}
      </View>

      {/* Écran d'attente : la génération peut prendre jusqu'à une minute. */}
      <Modal visible={generating} transparent animationType="fade">
        <View style={styles.overlay}>
          <View style={styles.overlayCard}>
            <ActivityIndicator size="large" color={colors.primary} />
            <Text style={styles.overlayTitle}>Le chef réfléchit…</Text>
            <Text style={styles.overlayText}>Il regarde ce qui périme en premier dans ton frigo. Ça peut prendre jusqu'à une minute.</Text>
          </View>
        </View>
      </Modal>
    </Screen>
  );
}

const styles = StyleSheet.create({
  title: { fontSize: 22, fontWeight: '700', color: colors.text },
  text: { fontSize: 15, color: colors.mutedText },
  section: { gap: spacing.sm, marginTop: spacing.md },
  subtitle: { fontSize: 17, fontWeight: '700', color: colors.text },
  hint: { fontSize: 13, color: colors.mutedText },
  generateRow: { flexDirection: 'row', gap: spacing.sm, alignItems: 'center' },
  generateButton: { flex: 1 },
  devButton: {
    width: 48,
    height: 48,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: colors.border,
    alignItems: 'center',
    justifyContent: 'center',
  },
  devButtonText: { fontSize: 22 },
  quota: { fontSize: 13, color: colors.mutedText, textAlign: 'center' },
  historyRow: {
    paddingVertical: spacing.sm,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: colors.border,
    gap: 2,
  },
  historyTitle: { fontSize: 16, fontWeight: '600', color: colors.text },
  historyMeta: { fontSize: 13, color: colors.mutedText },
  overlay: { flex: 1, backgroundColor: 'rgba(0,0,0,0.4)', justifyContent: 'center', padding: spacing.lg },
  overlayCard: { backgroundColor: colors.background, borderRadius: 12, padding: spacing.lg, gap: spacing.md, alignItems: 'center' },
  overlayTitle: { fontSize: 18, fontWeight: '700', color: colors.text },
  overlayText: { fontSize: 14, color: colors.mutedText, textAlign: 'center' },
});

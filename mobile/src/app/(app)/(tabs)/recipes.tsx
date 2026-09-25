import * as Clipboard from 'expo-clipboard';
import { router, useFocusEffect } from 'expo-router';
import { useCallback, useEffect, useRef, useState } from 'react';
import { Alert, Pressable, Text, View } from 'react-native';
import { api } from '@/api/client';
import { asApiError } from '@/api/errors';
import type { GenerateRecipeRequest, MealRestriction, Profile, Recipe, RecipeQuota } from '@/api/types';
import { useAuth } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { Card } from '@/components/Card';
import { EmptyState } from '@/components/EmptyState';
import { ChoiceChips } from '@/components/ChoiceChips';
import { ErrorBanner } from '@/components/ErrorBanner';
import { Bot, Star } from '@/components/icons/lucide';
import { MultiChoiceChips } from '@/components/MultiChoiceChips';
import { Screen } from '@/components/Screen';
import { Stepper } from '@/components/Stepper';
import { WaitingOverlay } from '@/components/WaitingOverlay';
import { WarningNote } from '@/components/WarningNote';
import { useHousehold } from '@/features/household/useHousehold';
import { toggle } from '@/features/profile/onboarding';
import { TastesSheet } from '@/features/profile/TastesSheet';
import { loadTastesPromptSeen, markTastesPromptSeen, shouldAskTastes } from '@/features/profile/tastesPrompt';
import {
  allergyWarning,
  encodeRestrictions,
  guestsLabel,
  loadLastDiners,
  maxGuests,
  MEAL_RESTRICTION_OPTIONS,
  restoreDiners,
  saveLastDiners,
} from '@/features/recipes/meal';
import { formatPrepTime, quotaLabel, toggleDiner } from '@/features/recipes/rules';
import { makeStyles, useTheme } from '@/theme';
import { formatShortDate } from '@/utils/dates';

/**
 * Recettes proposées par l'IA avec les produits du frigo, en priorité ceux qui périment
 * en premier. Quota : 3 par jour et par personne.
 */
export default function RecipesScreen() {
  const theme = useTheme();
  const styles = useStyles();
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
  // Question sur les goûts, posée une fois avant la première recette.
  const [tastesOpen, setTastesOpen] = useState(false);
  const [tastesProfile, setTastesProfile] = useState<Profile | null>(null);
  const [tastesSaving, setTastesSaving] = useState(false);
  const [tastesError, setTastesError] = useState<string | null>(null);
  // Recette à lancer une fois le panneau refermé (deux fenêtres à la fois posent problème sur iOS).
  const generateWhenHidden = useRef(false);
  // Invités sans nom et contraintes pour ce repas : jamais mémorisés, remis à zéro après chaque recette.
  const [guests, setGuests] = useState(0);
  const [restrictions, setRestrictions] = useState<MealRestriction[]>([]);
  const [restrictionsOpen, setRestrictionsOpen] = useState(false);

  const userId = user?.id ?? null;
  const memberIds = (household?.members ?? []).map((m) => m.userId);
  const memberKey = memberIds.join(',');

  // Dernière sélection de convives (sur ce téléphone), limitée aux membres actuels du foyer.
  useEffect(() => {
    if (!userId || !householdId) return;
    let active = true;
    void loadLastDiners(userId, householdId).then((saved) => {
      if (active) setDiners(restoreDiners(saved, memberKey ? memberKey.split(',') : [], userId));
    });
    return () => {
      active = false;
    };
  }, [userId, householdId, memberKey]);

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

  const request: GenerateRecipeRequest = { dinerUserIds: diners, servings: null, guests, mealRestrictions: restrictions };
  const warning = allergyWarning(restrictions);

  function toggleMealDiner(id: string) {
    const next = toggleDiner(diners, id);
    setDiners(next);
    // Assez de place à table : les invités en trop sont retirés.
    setGuests((current) => Math.min(current, maxGuests(next.length)));
    void saveLastDiners(user!.id, householdId!, next);
  }

  function changeGuests(value: number) {
    // Premier invité : les contraintes du repas se déplient d'elles-mêmes.
    if (guests === 0 && value > 0) setRestrictionsOpen(true);
    setGuests(value);
  }

  async function generate() {
    setGenerating(true);
    setError(null);
    try {
      const recipe = await api.recipes.generate(householdId!, request);
      // Contraintes d'allergène : transmises à l'écran de la recette pour l'avertissement renforcé,
      // par la navigation seulement (jamais enregistrées).
      router.push({
        pathname: '/recipe/[id]',
        params: warning ? { id: recipe.id, restrictions: encodeRestrictions(restrictions) } : { id: recipe.id },
      });
      // Repas suivant : de nouveau sans invités ni contraintes.
      setGuests(0);
      setRestrictions([]);
      setRestrictionsOpen(false);
    } catch (e) {
      setError(asApiError(e).message);
    } finally {
      setGenerating(false);
      void load();
    }
  }

  /** Première recette : on demande d'abord les goûts (une seule fois). Sinon, on lance la recette. */
  async function onGeneratePress() {
    if (!(await loadTastesPromptSeen(user!.id))) {
      try {
        const profile = await api.profile.get();
        if (profile && shouldAskTastes(profile, false)) {
          setTastesProfile(profile);
          setTastesError(null);
          setTastesOpen(true);
          return;
        }
        // Goûts déjà renseignés (dans Profil) : inutile de poser la question.
        await markTastesPromptSeen(user!.id);
      } catch {
        // Profil illisible : on ne bloque pas la recette pour autant.
      }
    }
    void generate();
  }

  async function saveTastesAndGenerate() {
    if (!tastesProfile) return;
    setTastesSaving(true);
    setTastesError(null);
    try {
      await api.profile.save(tastesProfile);
      await markTastesPromptSeen(user!.id);
      generateWhenHidden.current = true;
      setTastesOpen(false);
    } catch (e) {
      setTastesError(asApiError(e).message);
    } finally {
      setTastesSaving(false);
    }
  }

  async function tastesLater() {
    await markTastesPromptSeen(user!.id);
    generateWhenHidden.current = true;
    setTastesOpen(false);
  }

  function onTastesHidden() {
    if (generateWhenHidden.current) {
      generateWhenHidden.current = false;
      void generate();
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

      <View style={styles.section}>
        <Text style={styles.subtitle}>Qui mange ?</Text>
        {members.length > 1 ? (
          <>
            <MultiChoiceChips
              options={members.map((m) => ({
                value: m.userId,
                label: m.userId === user.id ? `${m.displayName} (toi)` : m.displayName,
              }))}
              values={diners}
              onToggle={toggleMealDiner}
            />
            <Text style={styles.hint}>Les régimes et allergies de chacun sont respectés, sans être dévoilés.</Text>
          </>
        ) : null}
        <View style={styles.guestsRow}>
          <View style={styles.guestsText}>
            <Text style={styles.guestsLabel}>Invités</Text>
            <Text style={styles.hint}>Sans nom : une portion de plus chacun.</Text>
          </View>
          <Stepper
            value={guests}
            min={0}
            max={maxGuests(diners.length)}
            onChange={changeGuests}
            valueLabel={guestsLabel(guests)}
            incrementLabel="Un invité de plus"
            decrementLabel="Un invité de moins"
          />
        </View>

        <Pressable
          onPress={() => setRestrictionsOpen((open) => !open)}
          accessibilityRole="button"
          accessibilityState={{ expanded: restrictionsOpen }}
          hitSlop={8}
        >
          <Text style={styles.link}>
            Contraintes pour ce repas{restrictions.length > 0 ? ` (${restrictions.length})` : ''} {restrictionsOpen ? '▴' : '▾'}
          </Text>
        </Pressable>
        {restrictionsOpen ? (
          <>
            <MultiChoiceChips
              options={MEAL_RESTRICTION_OPTIONS}
              values={restrictions}
              onToggle={(value) => setRestrictions((current) => toggle(current, value))}
            />
            <Text style={styles.hint}>Pour ce repas seulement : rien n'est enregistré.</Text>
          </>
        ) : null}
        {warning ? <WarningNote strong>{warning}</WarningNote> : null}
      </View>

      <ErrorBanner message={error ?? undefined} />

      <View style={styles.generateRow}>
        <View style={styles.generateButton}>
          <Button
            title="Proposer une recette"
            onPress={() => void onGeneratePress()}
            disabled={noQuotaLeft || generating || tastesOpen}
          />
        </View>
        {__DEV__ ? (
          <Pressable
            onPress={() => void copyPrompt()}
            style={styles.devButton}
            accessibilityRole="button"
            accessibilityLabel="Copier le prompt (outil de développement)"
          >
            <Bot size={24} strokeWidth={2} color={theme.colors.ink} />
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
          <EmptyState
            illustration="pasta"
            title={view === 'recent' ? 'Aucune recette ces 30 derniers jours' : 'Pas encore de favori'}
            text={
              view === 'recent'
                ? 'Touche « Proposer une recette » : elle partira de ce qui périme en premier.'
                : 'Mets une étoile sur une recette pour la garder dans le carnet du foyer.'
            }
          />
        ) : null}
        {(view === 'recent' ? history : favorites).map((recipe) => (
          <Card
            key={recipe.id}
            onPress={() => router.push({ pathname: '/recipe/[id]', params: { id: recipe.id } })}
            accessibilityLabel={`${recipe.title}${recipe.favoriteCount > 0 ? ', en favori' : ''}`}
            style={styles.historyCard}
          >
            <View style={styles.historyText}>
              <Text style={styles.historyTitle}>{recipe.title}</Text>
              <Text style={styles.historyMeta}>
                {formatPrepTime(recipe.prepMinutes)} · {formatShortDate(recipe.createdAt.slice(0, 10))}
              </Text>
            </View>
            {recipe.favoriteCount > 0 ? (
              <Star size={20} strokeWidth={2} color={theme.palette.citron.text} fill={theme.palette.citron.base} />
            ) : null}
          </Card>
        ))}
      </View>

      <TastesSheet
        visible={tastesOpen}
        profile={tastesProfile}
        onChange={setTastesProfile}
        onSave={() => void saveTastesAndGenerate()}
        onLater={() => void tastesLater()}
        onClose={() => setTastesOpen(false)}
        onHidden={onTastesHidden}
        saving={tastesSaving}
        error={tastesError}
      />

      {/* Écran d'attente : la génération peut prendre jusqu'à une minute. */}
      <WaitingOverlay
        visible={generating}
        illustration="aiPot"
        title="Le chef réfléchit…"
        text="Il regarde ce qui périme en premier dans ton frigo. Ça peut prendre jusqu'à une minute."
      />
    </Screen>
  );
}

const useStyles = makeStyles((t) => ({
  title: { ...t.type.title2, color: t.colors.ink },
  text: { ...t.type.body, color: t.colors.ink2 },
  section: { gap: t.space.sm, marginTop: t.space.md },
  subtitle: { ...t.type.title3, color: t.colors.ink },
  hint: { ...t.type.caption, color: t.colors.ink3 },
  guestsRow: { flexDirection: 'row', alignItems: 'center', gap: t.space.md },
  guestsText: { flex: 1, gap: t.space.xxs },
  guestsLabel: { ...t.type.bodyBold, color: t.colors.ink },
  link: { ...t.type.callout, color: t.colors.primaryText },
  generateRow: { flexDirection: 'row', gap: t.space.sm, alignItems: 'flex-start' },
  generateButton: { flex: 1 },
  // Outil de développement : même hauteur que le bouton à côté (sans ombre, ce n'est pas une action).
  devButton: {
    width: 52,
    height: 52,
    borderRadius: t.radius.md,
    borderWidth: t.borderWidth.hairline,
    borderColor: t.colors.line,
    backgroundColor: t.colors.surface,
    alignItems: 'center',
    justifyContent: 'center',
  },
  quota: { ...t.type.caption, color: t.colors.ink3, textAlign: 'center' },
  historyCard: { flexDirection: 'row', alignItems: 'center', gap: t.space.sm },
  historyText: { flex: 1, gap: 2 },
  historyTitle: { ...t.type.card, color: t.colors.ink },
  historyMeta: { ...t.type.caption, color: t.colors.ink3 },
}));

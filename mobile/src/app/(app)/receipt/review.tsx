import { router, useNavigation } from 'expo-router';
import { useEffect, useMemo, useRef, useState } from 'react';
import { ActivityIndicator, Alert, Pressable, StyleSheet, Switch, Text, View } from 'react-native';
import { api } from '@/api/client';
import { asApiError } from '@/api/errors';
import type { Category } from '@/api/types';
import { useAuth } from '@/auth/AuthContext';
import { Button } from '@/components/Button';
import { ChoiceChips } from '@/components/ChoiceChips';
import { DateField } from '@/components/DateField';
import { ErrorBanner } from '@/components/ErrorBanner';
import { Screen } from '@/components/Screen';
import { TextField } from '@/components/TextField';
import { CategoryPicker } from '@/features/inventory/CategoryPicker';
import { QuantityPicker } from '@/features/inventory/QuantityPicker';
import { estimateExpiry, formatQuantity, parseQuantity, UNITS, unitLabel } from '@/features/inventory/rules';
import { useCategories } from '@/features/inventory/useCategories';
import { askReminderPermission } from '@/features/notifications/reminders';
import { clearPendingScan, getPendingScan } from '@/features/receipts/pendingScan';
import {
  addButtonLabel,
  apiErrorsByLine,
  lineExpiresOn,
  skippedLabel,
  toRequests,
  toReviewLines,
  validateLines,
  type ReviewLine,
} from '@/features/receipts/rules';
import { colors, spacing } from '@/theme';
import { addDays, formatShortDate, toLocalDateString } from '@/utils/dates';

/**
 * Validation OBLIGATOIRE d'un ticket lu par l'IA (règle du projet) : l'utilisateur coche,
 * corrige ou écarte chaque ligne avant l'ajout au frigo, en une seule fois.
 */
export default function ReceiptReviewScreen() {
  const { state } = useAuth();
  const householdId = state.status === 'signedIn' ? state.user.householdId : null;
  const userId = state.status === 'signedIn' ? state.user.id : null;
  const { categories, byId: categoriesById } = useCategories();
  const navigation = useNavigation();
  const today = toLocalDateString();

  // Lecture passée par l'écran précédent (voir pendingScan.ts).
  const [scan] = useState(getPendingScan);
  const [lines, setLines] = useState<ReviewLine[]>(() => toReviewLines(scan?.lines ?? []));
  const [purchasedOn, setPurchasedOn] = useState(scan?.purchasedOn ?? today);
  const [isPersonal, setIsPersonal] = useState(false);
  const [expandedKey, setExpandedKey] = useState<string | null>(null);
  const [lineErrors, setLineErrors] = useState<Record<string, string>>({});
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  // Une fois les produits ajoutés, on quitte l'écran sans demander de confirmation.
  const done = useRef(false);

  useEffect(() => () => clearPendingScan(), []);

  const selectedCount = useMemo(() => lines.filter((l) => l.selected).length, [lines]);

  // Quitter sans ajouter perd le travail de relecture (et le scan reste décompté) : on confirme.
  useEffect(
    () =>
      navigation.addListener('beforeRemove', (event) => {
        if (done.current || selectedCount === 0) return;
        event.preventDefault();
        Alert.alert('Abandonner ce ticket ?', 'Les produits ne seront pas ajoutés au frigo.', [
          { text: 'Continuer la relecture', style: 'cancel' },
          { text: 'Abandonner', style: 'destructive', onPress: () => navigation.dispatch(event.data.action) },
        ]);
      }),
    [navigation, selectedCount],
  );

  function update(key: string, changes: Partial<ReviewLine>) {
    setLines((current) => current.map((l) => (l.key === key ? { ...l, ...changes } : l)));
    setLineErrors(({ [key]: _removed, ...rest }) => rest);
  }

  async function submit() {
    if (!householdId) return;
    setError(null);
    const errors = validateLines(lines);
    setLineErrors(errors);
    const firstError = lines.find((l) => errors[l.key]);
    if (firstError) {
      setExpandedKey(firstError.key);
      return;
    }

    setSubmitting(true);
    try {
      await api.inventory.createMany(householdId, toRequests(lines, purchasedOn, isPersonal));
      done.current = true;
      router.back();
      // Produits ajoutés : c'est le bon moment pour proposer les rappels de péremption.
      if (userId) void askReminderPermission(householdId, userId);
    } catch (e) {
      const apiError = asApiError(e);
      const byLine = apiErrorsByLine(apiError, lines);
      setLineErrors(byLine);
      const firstKey = lines.find((l) => byLine[l.key])?.key;
      if (firstKey) {
        setExpandedKey(firstKey);
      } else {
        setError(apiError.message);
      }
    } finally {
      setSubmitting(false);
    }
  }

  if (!scan) {
    // Arrivée directe sur l'écran (ex. rechargement de l'app en développement).
    return (
      <Screen hasHeader>
        <Text style={styles.title}>Aucun ticket à valider</Text>
        <Button title="Scanner un ticket" onPress={() => router.replace('/receipt/scan')} />
      </Screen>
    );
  }

  if (!categories) {
    return (
      <Screen hasHeader>
        <ActivityIndicator style={styles.loader} size="large" color={colors.primary} />
      </Screen>
    );
  }

  const skipped = skippedLabel(scan.skippedLineCount);

  if (lines.length === 0) {
    return (
      <Screen hasHeader>
        <Text style={styles.title}>Aucun produit reconnu</Text>
        <Text style={styles.text}>
          Réessaie avec une photo plus nette, ticket bien à plat et éclairé, ou ajoute tes produits à la main.
        </Text>
        {skipped ? <Text style={styles.hint}>{skipped}</Text> : null}
        <Button title="Reprendre une photo" onPress={() => router.replace('/receipt/scan')} />
        <Button title="Saisir à la main" variant="secondary" onPress={() => router.replace('/item/new')} />
      </Screen>
    );
  }

  return (
    <Screen hasHeader>
      <Text style={styles.title}>Vérifie tes courses</Text>
      <Text style={styles.text}>
        Décoche ce que tu ne veux pas ajouter, et touche un produit pour le corriger. Les dates sont estimées : corrige-les
        avec celles des emballages si besoin.
      </Text>
      {skipped ? <Text style={styles.hint}>{skipped}</Text> : null}

      <DateField
        label="Date d'achat"
        value={purchasedOn}
        onChange={setPurchasedOn}
        shortcuts={[
          { label: 'Aujourd\'hui', value: today },
          { label: 'Hier', value: addDays(today, -1) },
        ]}
        maximumDate={addDays(today, 1)}
        hint={scan.purchaseDateFromReceipt ? 'Lue sur le ticket.' : 'Date absente du ticket : aujourd\'hui par défaut.'}
      />

      <View style={styles.switchRow}>
        <View style={styles.switchText}>
          <Text style={styles.switchLabel}>Produits perso</Text>
          <Text style={styles.hint}>Pour tous les produits de ce ticket : visibles par le foyer, modifiables par toi seul.</Text>
        </View>
        <Switch value={isPersonal} onValueChange={setIsPersonal} />
      </View>

      <View style={styles.list}>
        {lines.map((line) => (
          <LineCard
            key={line.key}
            line={line}
            category={categoriesById.get(line.categoryId)}
            categories={categories}
            purchasedOn={purchasedOn}
            today={today}
            expanded={expandedKey === line.key}
            error={lineErrors[line.key]}
            onToggleExpanded={() => setExpandedKey((current) => (current === line.key ? null : line.key))}
            onChange={(changes) => update(line.key, changes)}
          />
        ))}
      </View>

      <ErrorBanner message={error ?? undefined} />
      <Button
        title={addButtonLabel(selectedCount)}
        onPress={() => void submit()}
        disabled={selectedCount === 0}
        loading={submitting}
      />
    </Screen>
  );
}

type LineCardProps = {
  line: ReviewLine;
  category: Category | undefined;
  categories: Category[];
  purchasedOn: string;
  today: string;
  expanded: boolean;
  error: string | undefined;
  onToggleExpanded: () => void;
  onChange: (changes: Partial<ReviewLine>) => void;
};

/** Une ligne du ticket : case à cocher, résumé, et formulaire de correction une fois ouverte. */
function LineCard({ line, category, categories, purchasedOn, today, expanded, error, onToggleExpanded, onChange }: LineCardProps) {
  const expiresOn = lineExpiresOn(line, purchasedOn, category);
  const quantity = parseQuantity(line.quantityText);
  const summary = [
    quantity !== null ? formatQuantity(quantity, line.unit) : line.quantityText,
    category?.name,
    expiresOn ? `avant le ${formatShortDate(expiresOn, today)}` : null,
  ]
    .filter(Boolean)
    .join(' · ');

  return (
    <View style={[styles.card, !line.selected && styles.cardUnselected, error ? styles.cardError : null]}>
      <View style={styles.cardRow}>
        <Pressable
          onPress={() => onChange({ selected: !line.selected })}
          accessibilityRole="checkbox"
          accessibilityState={{ checked: line.selected }}
          accessibilityLabel={`Ajouter ${line.name || 'ce produit'}`}
          hitSlop={8}
          style={[styles.checkbox, line.selected && styles.checkboxChecked]}
        >
          {line.selected ? <Text style={styles.checkmark}>✓</Text> : null}
        </Pressable>
        <Pressable
          onPress={onToggleExpanded}
          style={styles.cardContent}
          accessibilityRole="button"
          accessibilityHint={expanded ? 'Fermer la correction' : 'Corriger ce produit'}
        >
          <Text style={styles.cardName}>{line.name || 'Sans nom'}</Text>
          <Text style={styles.cardMeta}>{summary}</Text>
          <Text style={styles.cardReceipt}>Ticket : {line.receiptText}</Text>
        </Pressable>
      </View>
      {error ? <Text style={styles.error}>{error}</Text> : null}

      {expanded ? (
        <View style={styles.editor}>
          <TextField label="Nom du produit" value={line.name} onChangeText={(name) => onChange({ name })} maxLength={100} />
          <CategoryPicker categories={categories} value={line.categoryId} onChange={(categoryId) => onChange({ categoryId })} />
          <QuantityPicker value={line.quantityText} onChange={(quantityText) => onChange({ quantityText })} />
          <ChoiceChips
            options={UNITS.map((u) => ({ value: u, label: unitLabel(u) }))}
            value={line.unit}
            onChange={(unit) => onChange({ unit })}
          />
          {expiresOn ? (
            <DateField
              label="À consommer avant"
              value={expiresOn}
              onChange={(manualExpiresOn) => onChange({ manualExpiresOn })}
              shortcuts={[
                { label: 'Aujourd\'hui', value: today },
                { label: '+3 j', value: addDays(today, 3) },
                { label: '+1 sem.', value: addDays(today, 7) },
              ]}
              hint={line.manualExpiresOn === null && category ? `Estimée d'après la catégorie « ${category.name} ».` : undefined}
            />
          ) : null}
          {line.manualExpiresOn !== null && category ? (
            <Button
              title={`Revenir à l'estimation (${formatShortDate(estimateExpiry(purchasedOn, category), today)})`}
              variant="secondary"
              onPress={() => onChange({ manualExpiresOn: null })}
            />
          ) : null}
          <Button title="Terminé" variant="secondary" onPress={onToggleExpanded} />
        </View>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  title: { fontSize: 22, fontWeight: '700', color: colors.text },
  text: { fontSize: 15, color: colors.mutedText },
  hint: { fontSize: 13, color: colors.mutedText },
  loader: { marginTop: spacing.xl },
  switchRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  switchText: { flex: 1, gap: spacing.xs },
  switchLabel: { fontSize: 16, fontWeight: '600', color: colors.text },
  list: { gap: spacing.sm },
  card: { borderWidth: 1, borderColor: colors.border, borderRadius: 8, padding: spacing.md, gap: spacing.sm },
  cardUnselected: { opacity: 0.5 },
  cardError: { borderColor: colors.error },
  cardRow: { flexDirection: 'row', gap: spacing.md, alignItems: 'flex-start' },
  checkbox: {
    width: 26,
    height: 26,
    borderRadius: 6,
    borderWidth: 2,
    borderColor: colors.border,
    alignItems: 'center',
    justifyContent: 'center',
  },
  checkboxChecked: { backgroundColor: colors.primary, borderColor: colors.primary },
  checkmark: { color: '#FFFFFF', fontSize: 16, fontWeight: '700' },
  cardContent: { flex: 1, gap: 2 },
  cardName: { fontSize: 16, fontWeight: '600', color: colors.text },
  cardMeta: { fontSize: 14, color: colors.text },
  cardReceipt: { fontSize: 12, color: colors.mutedText },
  error: { fontSize: 13, color: colors.error },
  editor: { gap: spacing.md, marginTop: spacing.sm },
});

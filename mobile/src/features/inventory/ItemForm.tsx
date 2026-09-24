import { useMemo, useState } from 'react';
import { Switch, Text, View } from 'react-native';
import type { ApiError } from '@/api/errors';
import type { Category, InventoryItem, QuantityUnit, SaveInventoryItemRequest } from '@/api/types';
import { Button } from '@/components/Button';
import { DateField } from '@/components/DateField';
import { ErrorBanner } from '@/components/ErrorBanner';
import { TextField } from '@/components/TextField';
import { makeStyles, useTheme } from '@/theme';
import { addDays, formatShortDate, toLocalDateString } from '@/utils/dates';
import { CategoryPicker } from './CategoryPicker';
import { QuantityPicker } from './QuantityPicker';
import { estimateExpiry, parseQuantity, toQuantityText } from './rules';

// Valeurs de départ du formulaire (produit existant, ou suggestion après un scan).
export type ItemFormInitial = Partial<
  Pick<InventoryItem, 'name' | 'categoryId' | 'quantity' | 'unit' | 'purchasedOn' | 'barcode'>
> & {
  // Date saisie par l'utilisateur ; null/absent = date estimée d'après la catégorie.
  manualExpiresOn?: string | null;
  isPersonal?: boolean;
};

type Props = {
  categories: Category[];
  initial?: ItemFormInitial;
  submitLabel: string;
  onSubmit: (request: SaveInventoryItemRequest) => Promise<void>;
  error: ApiError | null;
  // Produit perso d'un autre membre : consultation seule.
  readOnly?: boolean;
};

/**
 * Formulaire d'un produit, partagé par l'ajout et la modification.
 * La date de péremption est estimée d'après la catégorie tant que l'utilisateur
 * n'en a pas choisi une : on envoie alors null et c'est l'API qui fait le calcul.
 */
export function ItemForm({ categories, initial, submitLabel, onSubmit, error, readOnly = false }: Props) {
  const theme = useTheme();
  const styles = useStyles();
  const today = toLocalDateString();

  const [name, setName] = useState(initial?.name ?? '');
  const [categoryId, setCategoryId] = useState<number | null>(initial?.categoryId ?? null);
  const [quantityText, setQuantityText] = useState(
    initial?.quantity !== undefined ? toQuantityText(initial.quantity) : '1',
  );
  const [unit, setUnit] = useState<QuantityUnit>(initial?.unit ?? 'Piece');
  const [purchasedOn, setPurchasedOn] = useState(initial?.purchasedOn ?? today);
  const [manualExpiresOn, setManualExpiresOn] = useState<string | null>(initial?.manualExpiresOn ?? null);
  const [isPersonal, setIsPersonal] = useState(initial?.isPersonal ?? false);
  const [localErrors, setLocalErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);

  const category = categories.find((c) => c.id === categoryId);
  const estimated = category ? estimateExpiry(purchasedOn, category) : null;
  const expiresOn = manualExpiresOn ?? estimated;

  const expiryShortcuts = useMemo(
    () => [
      { label: 'Aujourd\'hui', value: today },
      { label: '+3 j', value: addDays(today, 3) },
      { label: '+1 sem.', value: addDays(today, 7) },
    ],
    [today],
  );

  // Erreur locale (avant envoi) en priorité, sinon erreur renvoyée par l'API.
  const fieldError = (field: string) => localErrors[field] ?? error?.fieldError(field);
  const hasFieldErrors = Object.keys(localErrors).length > 0 || (error && Object.keys(error.fieldErrors).length > 0);

  async function handleSubmit() {
    // Vérifications immédiates, pour ne pas attendre l'aller-retour réseau.
    const errors: Record<string, string> = {};
    const quantity = parseQuantity(quantityText);
    if (!name.trim()) errors.Name = 'Le nom du produit est obligatoire.';
    if (!categoryId) errors.CategoryId = 'Choisis une catégorie.';
    if (quantity === null) errors.Quantity = 'Quantité invalide (ex. 1, 0,5 ou 250).';
    setLocalErrors(errors);
    if (Object.keys(errors).length > 0 || !categoryId || quantity === null) {
      return;
    }

    setSubmitting(true);
    try {
      await onSubmit({
        name: name.trim(),
        categoryId,
        quantity,
        unit,
        purchasedOn,
        expiresOn: manualExpiresOn,
        barcode: initial?.barcode ?? null,
        isPersonal,
      });
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <View style={styles.container}>
      <ErrorBanner message={error && !hasFieldErrors ? error.message : undefined} />

      <TextField
        label="Nom du produit"
        placeholder="Ex. Yaourts nature"
        value={name}
        onChangeText={setName}
        error={fieldError('Name')}
        maxLength={100}
        editable={!readOnly}
      />

      <CategoryPicker
        categories={categories}
        value={categoryId}
        onChange={setCategoryId}
        error={fieldError('CategoryId')}
        disabled={readOnly}
      />

      <QuantityPicker
        value={quantityText}
        onChange={setQuantityText}
        unit={unit}
        onUnitChange={setUnit}
        error={fieldError('Quantity')}
        disabled={readOnly}
      />

      <DateField
        label="Date d'achat"
        value={purchasedOn}
        onChange={setPurchasedOn}
        shortcuts={[
          { label: 'Aujourd\'hui', value: today },
          { label: 'Hier', value: addDays(today, -1) },
        ]}
        maximumDate={addDays(today, 1)}
        error={fieldError('PurchasedOn')}
        disabled={readOnly}
      />

      {expiresOn ? (
        <DateField
          label="À consommer avant"
          value={expiresOn}
          onChange={setManualExpiresOn}
          shortcuts={expiryShortcuts}
          hint={
            manualExpiresOn === null && category
              ? `Estimée d'après la catégorie « ${category.name} ». Corrige-la avec la date imprimée sur l'emballage.`
              : undefined
          }
          error={fieldError('ExpiresOn')}
          disabled={readOnly}
        />
      ) : (
        <Text style={styles.hint}>La date de péremption sera estimée dès que tu auras choisi une catégorie.</Text>
      )}

      {manualExpiresOn !== null && estimated && !readOnly ? (
        <Button
          title={`Revenir à l'estimation (${formatShortDate(estimated)})`}
          variant="secondary"
          onPress={() => setManualExpiresOn(null)}
        />
      ) : null}

      <View style={styles.switchRow}>
        <View style={styles.switchText}>
          <Text style={styles.switchLabel}>Produit perso</Text>
          <Text style={styles.hint}>Visible par tout le foyer, mais modifiable par toi seul.</Text>
        </View>
        <Switch
          value={isPersonal}
          onValueChange={setIsPersonal}
          disabled={readOnly}
          trackColor={{ true: theme.colors.primary, false: theme.colors.line }}
          ios_backgroundColor={theme.colors.line}
        />
      </View>

      {readOnly ? null : <Button title={submitLabel} onPress={() => void handleSubmit()} loading={submitting} />}
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  container: { gap: t.space.md },
  hint: { ...t.type.caption, color: t.colors.ink3 },
  switchRow: { flexDirection: 'row', alignItems: 'center', gap: t.space.md },
  switchText: { flex: 1, gap: t.space.xxs },
  switchLabel: { ...t.type.bodyBold, color: t.colors.ink },
}));

import type { ApiError } from '@/api/errors';
import type { Category, QuantityUnit, ReceiptLine, ReceiptQuota, SaveInventoryItemRequest } from '@/api/types';
import { estimateExpiry, formatQuantity, parseQuantity } from '@/features/inventory/rules';

/**
 * Règles de la lecture de ticket, en fonctions pures (testables sans interface).
 */

export function receiptQuotaLabel(quota: ReceiptQuota): string {
  if (quota.remaining <= 0) {
    return 'Plus de scan de ticket pour aujourd\'hui : reviens demain !';
  }
  return quota.remaining === 1
    ? '1 scan de ticket restant aujourd\'hui'
    : `${quota.remaining} scans de ticket restants aujourd'hui`;
}

export function skippedLabel(count: number): string | null {
  if (count <= 0) {
    return null;
  }
  return count === 1
    ? '1 article ignoré (non alimentaire ou illisible).'
    : `${count} articles ignorés (non alimentaires ou illisibles).`;
}

export function addButtonLabel(selectedCount: number): string {
  if (selectedCount === 0) {
    return 'Aucun produit sélectionné';
  }
  return selectedCount === 1 ? 'Ajouter 1 produit au frigo' : `Ajouter ${selectedCount} produits au frigo`;
}

// ---------- Écran de validation ----------

/** Ligne telle que l'utilisateur la corrige (la quantité reste du texte pendant la saisie). */
export type ReviewLine = {
  key: string;
  receiptText: string;
  name: string;
  categoryId: number;
  quantityText: string;
  unit: QuantityUnit;
  // Date choisie par l'utilisateur ; null = estimée d'après la catégorie et la date d'achat.
  manualExpiresOn: string | null;
  selected: boolean;
  // Lecture du ticket, telle que renvoyée par l'API (pour le détail « 6 × 1 l »).
  read: Pick<ReceiptLine, 'quantity' | 'unit' | 'copies' | 'quantityPerCopy'>;
};

export function toReviewLines(lines: ReceiptLine[]): ReviewLine[] {
  return lines.map((line, index) => ({
    key: `line-${index}`,
    receiptText: line.receiptText,
    name: line.name,
    categoryId: line.categoryId,
    quantityText: String(line.quantity).replace('.', ','),
    unit: line.unit,
    manualExpiresOn: null,
    // Tout est coché : l'utilisateur décoche ce qu'il ne veut pas ajouter.
    selected: true,
    read: { quantity: line.quantity, unit: line.unit, copies: line.copies, quantityPerCopy: line.quantityPerCopy },
  }));
}

/**
 * Détail de la quantité lue sur le ticket (« 6 × 1 l »), pour comprendre le total et le
 * corriger. Seulement pour plusieurs exemplaires, et tant que l'utilisateur n'a changé ni la
 * quantité ni l'unité : le détail ne correspondrait plus à la ligne.
 */
export function copiesDetail(line: ReviewLine): string | null {
  const { read } = line;
  if (read.copies <= 1 || line.unit !== read.unit || parseQuantity(line.quantityText) !== read.quantity) {
    return null;
  }
  return `${read.copies} × ${formatQuantity(read.quantityPerCopy, read.unit)}`;
}

/**
 * Date de péremption affichée : celle de l'utilisateur, sinon l'estimation. Elle suit la date
 * d'achat et la catégorie, comme le calcul de l'API (qui fait foi à l'enregistrement).
 */
export function lineExpiresOn(line: ReviewLine, purchasedOn: string, category: Category | undefined): string | null {
  return line.manualExpiresOn ?? (category ? estimateExpiry(purchasedOn, category) : null);
}

/** Erreurs des lignes cochées, avant envoi : clé de la ligne → message. */
export function validateLines(lines: ReviewLine[]): Record<string, string> {
  const errors: Record<string, string> = {};
  for (const line of lines.filter((l) => l.selected)) {
    if (!line.name.trim()) {
      errors[line.key] = 'Le nom du produit est obligatoire.';
    } else if (parseQuantity(line.quantityText) === null) {
      errors[line.key] = 'Quantité invalide (ex. 1, 0,5 ou 250).';
    }
  }
  return errors;
}

/** Requêtes d'ajout des lignes cochées (à appeler après validateLines). */
export function toRequests(lines: ReviewLine[], purchasedOn: string, isPersonal: boolean): SaveInventoryItemRequest[] {
  return lines
    .filter((l) => l.selected)
    .map((line) => ({
      name: line.name.trim(),
      categoryId: line.categoryId,
      quantity: parseQuantity(line.quantityText) ?? 1,
      unit: line.unit,
      purchasedOn,
      // null : l'API estime la date, avec le même calcul que l'aperçu.
      expiresOn: line.manualExpiresOn,
      barcode: null,
      isPersonal,
    }));
}

/**
 * Erreurs renvoyées par l'API pour l'ajout groupé (« Items[2].Name »), rattachées aux lignes.
 * L'index est celui de la requête, qui ne contient que les lignes cochées.
 */
export function apiErrorsByLine(error: ApiError, lines: ReviewLine[]): Record<string, string> {
  const selected = lines.filter((l) => l.selected);
  const errors: Record<string, string> = {};
  for (const [field, messages] of Object.entries(error.fieldErrors)) {
    const match = /^Items\[(\d+)\]/i.exec(field);
    const line = match ? selected[Number(match[1])] : undefined;
    if (line && messages[0] && !errors[line.key]) {
      errors[line.key] = messages[0];
    }
  }
  return errors;
}

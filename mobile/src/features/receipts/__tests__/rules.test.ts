// Types de Jest chargés ici seulement : TypeScript 6 ne les inclut plus automatiquement,
// et on évite ainsi de les exposer au code de l'app.
/// <reference types="jest" />

jest.mock('expo-image-manipulator', () => ({ ImageManipulator: {}, SaveFormat: { JPEG: 'jpeg' } }));

import { ApiError } from '@/api/errors';
import type { Category, ReceiptLine } from '@/api/types';
import { resizeTarget } from '../image';
import {
  addButtonLabel,
  apiErrorsByLine,
  lineExpiresOn,
  receiptQuotaLabel,
  skippedLabel,
  toRequests,
  toReviewLines,
  validateLines,
} from '../rules';

const groundMeat: Category = { id: 1, code: 'ground-meat', name: 'Viande hachée', defaultShelfLifeDays: 1, expiryKind: 'UseBy' };

const line = (overrides: Partial<ReceiptLine> = {}): ReceiptLine => ({
  receiptText: 'STEAK HACHE X2',
  name: 'Steak haché',
  categoryId: 1,
  quantity: 2,
  unit: 'Piece',
  expiresOn: '2026-09-25',
  expiryKind: 'UseBy',
  ...overrides,
});

describe('resizeTarget', () => {
  it('ramène le plus grand côté à 1568 px, proportions conservées', () => {
    expect(resizeTarget(3024, 4032)).toEqual({ height: 1568 });
    expect(resizeTarget(4032, 3024)).toEqual({ width: 1568 });
  });

  it('ne touche pas une image déjà assez petite', () => {
    expect(resizeTarget(1000, 1568)).toBeNull();
  });
});

describe('libellés', () => {
  it('quota, articles ignorés et bouton d\'ajout', () => {
    const quota = { used: 1, limit: 3, remaining: 2, resetsAt: '' };
    expect(receiptQuotaLabel(quota)).toBe('2 scans de ticket restants aujourd\'hui');
    expect(receiptQuotaLabel({ ...quota, remaining: 0 })).toContain('reviens demain');
    expect(skippedLabel(0)).toBeNull();
    expect(skippedLabel(1)).toBe('1 article ignoré (non alimentaire ou illisible).');
    expect(addButtonLabel(0)).toBe('Aucun produit sélectionné');
    expect(addButtonLabel(3)).toBe('Ajouter 3 produits au frigo');
  });
});

describe('lignes à valider', () => {
  it('sont toutes cochées, avec une quantité au format français', () => {
    const [review] = toReviewLines([line({ quantity: 0.612, unit: 'Kilogram' })]);
    expect(review).toMatchObject({ selected: true, quantityText: '0,612', manualExpiresOn: null });
  });

  it('la date estimée suit la date d\'achat, sauf si l\'utilisateur en a choisi une', () => {
    const [review] = toReviewLines([line()]);
    expect(lineExpiresOn(review!, '2026-09-20', groundMeat)).toBe('2026-09-21');
    expect(lineExpiresOn({ ...review!, manualExpiresOn: '2026-10-01' }, '2026-09-20', groundMeat)).toBe('2026-10-01');
  });

  it('seules les lignes cochées sont vérifiées et envoyées', () => {
    const [a, b, c] = toReviewLines([line(), line({ name: 'Yaourt' }), line({ name: 'Lessive' })]);
    const lines = [{ ...a!, name: '  ' }, { ...b!, quantityText: 'beaucoup' }, { ...c!, selected: false, name: '' }];

    expect(validateLines(lines)).toEqual({
      'line-0': 'Le nom du produit est obligatoire.',
      'line-1': 'Quantité invalide (ex. 1, 0,5 ou 250).',
    });
  });

  it('les requêtes laissent l\'API estimer la date, sauf date choisie', () => {
    const [a, b] = toReviewLines([line(), line({ name: 'Yaourt', quantity: 0.5, unit: 'Kilogram' })]);
    const requests = toRequests([a!, { ...b!, manualExpiresOn: '2026-10-01' }], '2026-09-20', true);

    expect(requests).toEqual([
      expect.objectContaining({ name: 'Steak haché', quantity: 2, expiresOn: null, purchasedOn: '2026-09-20', isPersonal: true }),
      expect.objectContaining({ name: 'Yaourt', quantity: 0.5, unit: 'Kilogram', expiresOn: '2026-10-01' }),
    ]);
  });

  it('une erreur de l\'API « Items[1] » désigne la 2e ligne COCHÉE', () => {
    const [a, b, c] = toReviewLines([line(), line({ name: 'Décochée' }), line({ name: 'Yaourt' })]);
    const lines = [a!, { ...b!, selected: false }, c!];
    const error = new ApiError('Invalide', 400, 'inventory.validation', { 'Items[1].Name': ['Nom trop long.'] });

    expect(apiErrorsByLine(error, lines)).toEqual({ 'line-2': 'Nom trop long.' });
  });
});

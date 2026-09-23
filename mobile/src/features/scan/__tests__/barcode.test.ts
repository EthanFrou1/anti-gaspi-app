// Types de Jest chargés ici seulement : TypeScript 6 ne les inclut plus automatiquement,
// et on évite ainsi de les exposer au code de l'app.
/// <reference types="jest" />

import { hasValidCheckDigit, readFoodBarcode } from '../barcode';

describe('hasValidCheckDigit', () => {
  it.each([
    ['3017620422003', 'EAN-13 (Nutella)'],
    ['3033490004743', 'EAN-13 (skyr Danone)'],
    ['96385074', 'EAN-8'],
    ['036000291452', 'UPC-A'],
    ['10614141000415', 'GTIN-14'],
  ])('accepte %s (%s)', (code) => {
    expect(hasValidCheckDigit(code)).toBe(true);
  });

  it.each([
    ['3017620422004', 'dernier chiffre faux (lecture erronée)'],
    ['3017620422030', 'deux chiffres inversés'],
    ['1234567', 'trop court'],
    ['123456789012345', 'trop long'],
    ['30176204220A3', 'lettre'],
    ['', 'vide'],
  ])('refuse %s (%s)', (code) => {
    expect(hasValidCheckDigit(code)).toBe(false);
  });
});

describe('readFoodBarcode', () => {
  it.each([
    ['ean13', '3017620422003'],
    ['org.gs1.EAN-13', '3017620422003'], // format iOS
    ['EAN_13', '3017620422003'],
    ['ean8', '96385074'],
    ['upc_a', '036000291452'],
  ])('lit un code alimentaire de type %s', (type, data) => {
    expect(readFoodBarcode(type, data)).toBe(data);
  });

  it('ignore un QR code, même s\'il ne contient que des chiffres', () => {
    expect(readFoodBarcode('qr', '3017620422003')).toBeNull();
  });

  it('ignore une lecture dont la clé de contrôle est fausse', () => {
    expect(readFoodBarcode('ean13', '3017620422004')).toBeNull();
  });

  it('retire les espaces autour du code', () => {
    expect(readFoodBarcode('ean13', ' 3017620422003 ')).toBe('3017620422003');
  });
});

import type { BarcodeType } from 'expo-camera';

/**
 * Codes-barres des produits alimentaires : EAN-13 (le standard en Europe), EAN-8
 * (petits emballages) et UPC-A (produits américains ; iOS les signale d'ailleurs
 * souvent comme EAN-13 précédés d'un 0).
 * UPC-E (format compressé, rare en France) est volontairement exclu.
 */
export const FOOD_BARCODE_TYPES: BarcodeType[] = ['ean13', 'ean8', 'upc_a'];

/**
 * Vérifie la clé de contrôle d'un code GTIN (EAN-8, UPC-A, EAN-13, GTIN-14) :
 * le dernier chiffre est calculé à partir des autres. Un code mal lu par la caméra
 * (reflet, code abîmé) est ainsi rejeté avant même d'interroger l'API.
 */
export function hasValidCheckDigit(code: string): boolean {
  if (!/^\d{8,14}$/.test(code)) {
    return false;
  }
  const digits = code.split('').map(Number);
  const checkDigit = digits.pop()!;
  // En partant de la droite (hors clé), les chiffres sont pondérés alternativement 3, 1, 3, 1…
  const sum = digits
    .reverse()
    .reduce((total, digit, index) => total + digit * (index % 2 === 0 ? 3 : 1), 0);
  return (10 - (sum % 10)) % 10 === checkDigit;
}

/**
 * Transforme un résultat de scan en code exploitable, ou null s'il faut l'ignorer
 * (type non alimentaire, QR code, lecture erronée…).
 */
export function readFoodBarcode(type: string, data: string): string | null {
  const normalizedType = type.toLowerCase().replace(/[^a-z0-9]/g, '');
  // Selon la plateforme, le type arrive sous la forme « ean13 », « EAN-13 » ou « org.gs1.EAN-13 ».
  const accepted = ['ean13', 'ean8', 'upca', 'orggs1ean13', 'orggs1ean8', 'orggs1upca'];
  if (!accepted.includes(normalizedType)) {
    return null;
  }
  const code = data.trim();
  return hasValidCheckDigit(code) ? code : null;
}

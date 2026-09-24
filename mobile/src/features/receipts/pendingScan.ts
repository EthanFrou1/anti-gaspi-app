import type { ReceiptScan } from '@/api/types';

/**
 * Lecture de ticket en attente de validation, transmise de l'écran de prise de photo à
 * l'écran de validation.
 *
 * Pourquoi pas un paramètre de navigation ? Les paramètres d'expo-router sont des chaînes
 * placées dans l'URL de l'écran : jusqu'à 60 lignes de ticket n'y ont pas leur place.
 * Une simple variable en mémoire suffit (rien n'est écrit sur le téléphone).
 */
let pending: ReceiptScan | null = null;

export function setPendingScan(scan: ReceiptScan): void {
  pending = scan;
}

export function getPendingScan(): ReceiptScan | null {
  return pending;
}

export function clearPendingScan(): void {
  pending = null;
}

import type { Urgency as InventoryUrgency } from '@/features/inventory/rules';
import type { Urgency as BrandUrgency } from './tokens';

/**
 * Urgence calculée par l'inventaire (features/inventory/rules.ts, inchangé) → urgence de la
 * charte. Seul le nom diffère : « critical » (≤ 2 jours) s'appelle « urgent » dans la charte.
 */
export function brandUrgency(urgency: InventoryUrgency): BrandUrgency {
  return urgency === 'critical' ? 'urgent' : urgency;
}

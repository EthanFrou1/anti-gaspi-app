import type { AddAction } from './AddSheet';

/**
 * Écran ouvert par chaque action du panneau « + ».
 * - « push » : écran posé par-dessus les onglets (le retour ramène à l'onglet d'origine) ;
 * - « navigate » : changement d'onglet. « Proposer une recette » ouvre l'onglet Recettes SANS
 *   lancer la génération (elle consomme le quota et demande d'abord de choisir les convives).
 */
export function addActionTarget(action: AddAction) {
  switch (action) {
    case 'scanProduct':
      return { mode: 'push', href: '/item/scan' } as const;
    case 'manualEntry':
      return { mode: 'push', href: '/item/new' } as const;
    case 'scanReceipt':
      return { mode: 'push', href: '/receipt/scan' } as const;
    case 'suggestRecipe':
      return { mode: 'navigate', href: '/recipes' } as const;
    case 'goToHousehold':
      return { mode: 'navigate', href: '/household' } as const;
  }
}

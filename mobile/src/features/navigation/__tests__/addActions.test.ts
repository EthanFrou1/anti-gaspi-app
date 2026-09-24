// Types de Jest chargés ici seulement : TypeScript 6 ne les inclut plus automatiquement,
// et on évite ainsi de les exposer au code de l'app.
/// <reference types="jest" />

import { addActionTarget } from '../addActions';

describe('panneau « + »', () => {
  it('les ajouts s\'ouvrent par-dessus les onglets', () => {
    expect(addActionTarget('scanProduct')).toEqual({ mode: 'push', href: '/item/scan' });
    expect(addActionTarget('manualEntry')).toEqual({ mode: 'push', href: '/item/new' });
    expect(addActionTarget('scanReceipt')).toEqual({ mode: 'push', href: '/receipt/scan' });
  });

  it('« Proposer une recette » ouvre l\'onglet Recettes, sans lancer de génération', () => {
    expect(addActionTarget('suggestRecipe')).toEqual({ mode: 'navigate', href: '/recipes' });
  });

  it('sans foyer, le panneau renvoie vers l\'onglet Foyer', () => {
    expect(addActionTarget('goToHousehold')).toEqual({ mode: 'navigate', href: '/household' });
  });
});

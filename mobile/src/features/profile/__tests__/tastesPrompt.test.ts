// Types de Jest chargés ici seulement : TypeScript 6 ne les inclut plus automatiquement,
// et on évite ainsi de les exposer au code de l'app.
/// <reference types="jest" />

import AsyncStorage from '@react-native-async-storage/async-storage';
import { loadTastesPromptSeen, markTastesPromptSeen, shouldAskTastes } from '../tastesPrompt';

beforeEach(async () => {
  await AsyncStorage.clear();
});

describe('question sur les goûts avant la première recette', () => {
  it('n\'est posée que si elle n\'a jamais eu de réponse et que le profil n\'a aucun goût', () => {
    const empty = { dislikes: [], avoidSpicy: false };
    expect(shouldAskTastes(empty, false)).toBe(true);
    expect(shouldAskTastes(empty, true)).toBe(false);
    expect(shouldAskTastes({ dislikes: ['Olives'], avoidSpicy: false }, false)).toBe(false);
    expect(shouldAskTastes({ dislikes: [], avoidSpicy: true }, false)).toBe(false);
  });

  it('est mémorisée par utilisateur, sur ce téléphone', async () => {
    expect(await loadTastesPromptSeen('alice')).toBe(false);
    await markTastesPromptSeen('alice');
    expect(await loadTastesPromptSeen('alice')).toBe(true);
    // Un autre compte sur le même téléphone aura sa propre question.
    expect(await loadTastesPromptSeen('bob')).toBe(false);
  });

  it('n\'insiste pas si le stockage est illisible', async () => {
    jest.spyOn(AsyncStorage, 'getItem').mockRejectedValueOnce(new Error('stockage indisponible'));
    expect(await loadTastesPromptSeen('alice')).toBe(true);
  });
});

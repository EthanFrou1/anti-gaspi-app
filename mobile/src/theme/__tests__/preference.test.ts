// Types de Jest chargés ici seulement : TypeScript 6 ne les inclut plus automatiquement,
// et on évite ainsi de les exposer au code de l'app.
/// <reference types="jest" />

import AsyncStorage from '@react-native-async-storage/async-storage';
import { Appearance } from 'react-native';
import { applyThemePreference, isThemePreference, loadThemePreference, saveThemePreference } from '../preference';

beforeEach(async () => {
  await AsyncStorage.clear();
});

describe('préférence de thème', () => {
  it('« Automatique » par défaut, puis le choix enregistré', async () => {
    expect(await loadThemePreference()).toBe('system');
    await saveThemePreference('dark');
    expect(await loadThemePreference()).toBe('dark');
  });

  it('une valeur inconnue (ancienne version, stockage abîmé) redevient « Automatique »', async () => {
    await AsyncStorage.setItem('leftly.themePreference', 'violet');
    expect(await loadThemePreference()).toBe('system');
    expect(isThemePreference('light')).toBe(true);
    expect(isThemePreference(null)).toBe(false);
  });

  it('une erreur de lecture ne bloque pas l\'app', async () => {
    jest.spyOn(AsyncStorage, 'getItem').mockRejectedValueOnce(new Error('stockage indisponible'));
    expect(await loadThemePreference()).toBe('system');
  });

  it('« Automatique » rend la main au téléphone ; Clair et Sombre forcent le mode', () => {
    const setColorScheme = jest.spyOn(Appearance, 'setColorScheme').mockImplementation(() => undefined);
    applyThemePreference('system');
    applyThemePreference('dark');
    expect(setColorScheme.mock.calls).toEqual([['unspecified'], ['dark']]);
  });
});

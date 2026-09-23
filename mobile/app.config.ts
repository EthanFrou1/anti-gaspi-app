import type { ExpoConfig } from 'expo/config';

// Nom provisoire : c'est le SEUL endroit où il est défini côté mobile.
// Le reste de l'app le lit via src/config.ts (Constants.expoConfig.name).
const APP_NAME = 'Anti-Gaspi';

const config: ExpoConfig = {
  name: APP_NAME,
  slug: 'anti-gaspi-app',
  // Préfixe des liens profonds (ex. antigaspi://join/ABCD2345, prévu plus tard).
  scheme: 'antigaspi',
  version: '1.0.0',
  orientation: 'portrait',
  icon: './assets/icon.png',
  userInterfaceStyle: 'light',
  ios: {
    supportsTablet: true,
  },
  android: {
    adaptiveIcon: {
      backgroundColor: '#E6F4FE',
      foregroundImage: './assets/android-icon-foreground.png',
      backgroundImage: './assets/android-icon-background.png',
      monochromeImage: './assets/android-icon-monochrome.png',
    },
    predictiveBackGestureEnabled: false,
  },
  web: {
    favicon: './assets/favicon.png',
  },
  plugins: ['expo-router', 'expo-status-bar', 'expo-secure-store'],
  experiments: {
    // Vérifie à la compilation que les liens de navigation pointent vers des routes existantes.
    typedRoutes: true,
  },
};

export default config;

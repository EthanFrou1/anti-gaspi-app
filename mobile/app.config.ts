import type { ExpoConfig } from 'expo/config';

// Nom de l'app : c'est le SEUL endroit où il est défini côté mobile.
// Le reste de l'app le lit via src/config.ts (Constants.expoConfig.name).
const APP_NAME = 'Leftly';

// Un seul message pour l'appareil photo : expo-camera (codes-barres) et expo-image-picker
// (tickets de caisse) renseignent la même autorisation iOS, le dernier plugin l'emporterait.
const CAMERA_PERMISSION = `${APP_NAME} utilise l'appareil photo pour scanner les codes-barres de tes produits et photographier tes tickets de caisse.`;

const config: ExpoConfig = {
  name: APP_NAME,
  slug: 'leftly',
  // Préfixe des liens profonds (ex. leftly://join/ABCD2345, prévu plus tard).
  scheme: 'leftly',
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
  plugins: [
    'expo-router',
    'expo-status-bar',
    'expo-secure-store',
    [
      'expo-camera',
      {
        // Message affiché par iOS lors de la demande d'accès (build de l'app ; Expo Go
        // affiche son propre message).
        cameraPermission: CAMERA_PERMISSION,
        // On ne filme pas : ni micro sur iOS, ni permission RECORD_AUDIO sur Android.
        microphonePermission: false,
        recordAudioAndroid: false,
        barcodeScannerEnabled: true,
      },
    ],
    [
      'expo-image-picker',
      {
        cameraPermission: CAMERA_PERMISSION,
        photosPermission: `${APP_NAME} accède à la photo de ticket de caisse que tu choisis, pour lire les produits achetés.`,
        // Photos uniquement : ni micro sur iOS, ni permission RECORD_AUDIO sur Android.
        microphonePermission: false,
      },
    ],
  ],
  experiments: {
    // Vérifie à la compilation que les liens de navigation pointent vers des routes existantes.
    typedRoutes: true,
  },
};

export default config;

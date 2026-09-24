import type { ExpoConfig } from 'expo/config';

// Nom de l'app : c'est le SEUL endroit où il est défini côté mobile.
// Le reste de l'app le lit via src/config.ts (Constants.expoConfig.name).
const APP_NAME = 'Leftly';

// Un seul message pour l'appareil photo : expo-camera (codes-barres) et expo-image-picker
// (tickets de caisse) renseignent la même autorisation iOS, le dernier plugin l'emporterait.
// Fonds de la charte (tokens brand.appIcon et brand.splash) ; un test vérifie qu'ils correspondent.
export const BRAND_BACKGROUND = { light: '#FFF8F3', dark: '#150F1E' } as const;

const CAMERA_PERMISSION = `${APP_NAME} utilise l'appareil photo pour scanner les codes-barres de tes produits et photographier tes tickets de caisse.`;

const config: ExpoConfig = {
  name: APP_NAME,
  slug: 'leftly',
  // Préfixe des liens profonds (ex. leftly://join/ABCD2345, prévu plus tard).
  scheme: 'leftly',
  version: '1.0.0',
  orientation: 'portrait',
  icon: './assets/brand/app-icon/ios/AppIcon-1024.png',
  // TRANSITION : l'app reste en mode clair tant que tous les écrans ne sont pas migrés vers le
  // thème de la charte ; passera à 'automatic' (suivre le réglage du téléphone) à la fin.
  userInterfaceStyle: 'light',
  ios: {
    supportsTablet: true,
    // Icône sombre d'iOS 18 : la « bouchée » de la tuile laisse voir le fond sombre.
    icon: {
      light: './assets/brand/app-icon/ios/AppIcon-1024.png',
      dark: './assets/brand/app-icon/ios/AppIcon-1024-sombre.png',
    },
  },
  android: {
    // Icône adaptative : tuile au premier plan (dans la zone sûre), fond uni, et version
    // monochrome pour les icônes thématiques d'Android 13+.
    adaptiveIcon: {
      foregroundImage: './assets/brand/app-icon/android/ic_launcher_foreground.png',
      backgroundColor: BRAND_BACKGROUND.light,
      monochromeImage: './assets/brand/app-icon/android/ic_launcher_monochrome.png',
    },
    predictiveBackGestureEnabled: false,
  },
  web: {
    favicon: './assets/brand/logo/leftly-mark-1024.png',
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
      // Splash natif : logo centré sur le fond de la charte, clair ou sombre selon le téléphone.
      'expo-splash-screen',
      {
        image: './assets/brand/splash/splash-icon-1024.png',
        imageWidth: 112,
        backgroundColor: BRAND_BACKGROUND.light,
        dark: {
          image: './assets/brand/splash/splash-icon-1024.png',
          backgroundColor: BRAND_BACKGROUND.dark,
        },
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

# Leftly — livrables pour le développement

## Contenu

| Dossier | Contenu |
|---|---|
| `app-icon/ios/` | `AppIcon-1024.png` (clair) et `AppIcon-1024-sombre.png` (icône sombre iOS 18), carrés pleins, sans arrondi : iOS applique son propre masque. SVG sources à côté. |
| `app-icon/android/` | Icône adaptative en calques séparés, 1024 × 1024 : `ic_launcher_background` (fond uni #FFF8F3), `ic_launcher_foreground` (tuile croquée, fond transparent, dans la zone sûre Ø 66 dp sur 108 dp), `ic_launcher_monochrome` (icônes thématiques Android 13+). Aussi : `play-store-512.png` et `apercu-masque-rond.png`. |
| `splash/` | `splash-clair` / `splash-sombre` en 1290 × 2796 (PNG + SVG) et `splash-icon-1024.png` (logo seul, transparent). |
| `logo/` | Symbole, symbole mono (encre / blanc), version sur tangerine, wordmark et lockups horizontal / vertical, clair et sombre. Le texte « Leftly » est vectorisé (Fredoka 700) : aucune police n'est requise. |
| `illustrations/` | `mascotte/` (3 humeurs), `humeurs/` (steak × 5), `troupe/` (7 aliments), `decors/` (frigo, frigo vide, marmite IA, 3 plats). |
| `icons/` | `etat/` (5 icônes d'urgence) et `categorie/` (21 groupes de catégories, mapping dans `categoryGroups`). Grille 24, trait 2 px en `currentColor`. |
| `leftly-tokens.ts` | Design tokens React Native / Expo à jour. |

## Expo (`app.json`)

```json
{
  "expo": {
    "name": "Leftly",
    "icon": "./assets/app-icon/ios/AppIcon-1024.png",
    "ios": {
      "icon": {
        "light": "./assets/app-icon/ios/AppIcon-1024.png",
        "dark": "./assets/app-icon/ios/AppIcon-1024-sombre.png"
      }
    },
    "android": {
      "adaptiveIcon": {
        "foregroundImage": "./assets/app-icon/android/ic_launcher_foreground.png",
        "backgroundColor": "#FFF8F3",
        "monochromeImage": "./assets/app-icon/android/ic_launcher_monochrome.png"
      }
    },
    "plugins": [
      ["expo-splash-screen", {
        "image": "./assets/splash/splash-icon-1024.png",
        "imageWidth": 112,
        "backgroundColor": "#FFF8F3",
        "dark": { "image": "./assets/splash/splash-icon-1024.png", "backgroundColor": "#150F1E" }
      }]
    ]
  }
}
```

Les écrans `splash-*-1290x2796.png` montrent le rendu complet (logo + nom). Le splash natif Expo n'affiche qu'une image centrée : si vous voulez aussi le nom, utilisez l'image de la version clair / sombre en `resizeMode: "cover"`.

## SVG dans l'app

Avec `react-native-svg` + `react-native-svg-transformer`, les icônes héritent de la couleur via `color` (ou `stroke`) grâce à `currentColor` :

```tsx
import Flame from './assets/icons/etat/urgent-flame.svg';
<Flame width={16} height={16} color={urgency.urgent.light.fg} />
```

Le logo utilise un `<mask>`, qui est pris en charge par `react-native-svg` depuis la version 9.

# Mobile

App Expo (SDK 57) en React Native + TypeScript strict, navigation avec Expo Router.
Plateformes cibles : iOS et Android (le web n'est pas supporté : `expo-secure-store` n'y existe pas).

## Structure

```
mobile/
├── app.config.ts        → config Expo ; SEUL endroit où le nom de l'app est défini
├── .env.example         → modèle de configuration locale (URL de l'API)
└── src/
    ├── app/             → écrans (Expo Router : un fichier = une route)
    │   ├── _layout.tsx  → racine : session, écrans de chargement, routes protégées
    │   ├── (auth)/      → connexion, inscription (accessibles déconnecté)
    │   └── (app)/       → écrans de l'app (accessibles connecté)
    │       ├── onboarding.tsx → 5 questions après l'inscription (tant que le profil n'existe pas)
    │       ├── preferences.tsx → modification des préférences alimentaires
    │       ├── (tabs)/  → onglets Frigo, Recettes, Foyer, Compte
    │       ├── recipe/  → détail d'une recette, « J'ai cuisiné »
    │       └── item/    → ajout et modification d'un produit
    ├── api/             → client API : SEUL module qui appelle le serveur
    ├── auth/            → contexte de session, stockage sécurisé des jetons
    ├── components/      → composants d'interface réutilisables
    ├── features/        → logique et vues par fonctionnalité (household/, inventory/, notifications/, profile/, recipes/, scan/)
    ├── utils/           → utilitaires sans interface (dates « jour » en calendrier local)
    ├── config.ts        → nom de l'app, URL de l'API
    └── theme.ts         → couleurs et espacements provisoires
```

## Lancer en local

Prérequis : l'API tourne (voir [../api/README.md](../api/README.md)), Node.js, et
l'app Expo Go sur le téléphone ou un émulateur.

```bash
cd mobile
npm install
cp .env.example .env.local     # puis adapter EXPO_PUBLIC_API_URL
npx expo start
```

### Joindre l'API depuis le téléphone

`localhost` désigne l'appareil lui-même, pas ton PC. Selon le cas :

| Où tourne l'app | `EXPO_PUBLIC_API_URL` | Lancement de l'API |
|---|---|---|
| Émulateur Android | `http://10.0.2.2:5122` | `dotnet run --project src/Api` |
| Simulateur iOS (Mac) | `http://localhost:5122` | `dotnet run --project src/Api` |
| Téléphone réel (même Wi-Fi) | `http://<IP du PC>:5122` | `dotnet run --project src/Api --urls http://0.0.0.0:5122` |

Pour un téléphone réel, il faut aussi autoriser le port 5122 dans le pare-feu Windows
(voir [Dépannage](#dépannage)).
Après toute modification de `.env.local`, relancer `npx expo start --clear`.

## Dépannage

### Expo Go refuse d'ouvrir le projet (compte Expo)

Expo Go doit être connecté au **même compte Expo** que la CLI sur le PC.

```bash
npx expo whoami        # compte utilisé par la CLI
npx expo login         # se connecter si besoin
```

Puis, dans Expo Go : onglet Profil → se connecter avec ce même compte.

### Le téléphone ne joint pas l'API (pare-feu Windows)

Symptôme : l'app affiche « Impossible de joindre le serveur », alors que
`http://localhost:5122/openapi/v1.json` répond sur le PC. Windows bloque les
connexions entrantes, en particulier quand le Wi-Fi est classé « réseau public »
(cas fréquent au bureau ou dans un lieu public).

- **À la maison** : passer le Wi-Fi en profil « Privé » (Paramètres → Réseau et Internet
  → Wi-Fi → propriétés du réseau), puis autoriser le port pour ce profil uniquement,
  dans un PowerShell administrateur :

  ```powershell
  New-NetFirewallRule -DisplayName "API Leftly (dev)" -Direction Inbound `
    -Protocol TCP -LocalPort 5122 -Action Allow -Profile Private
  ```

- **Sur un réseau public (bureau, école…)** : ne pas ouvrir le port sur le profil
  « Public », sinon n'importe qui sur ce réseau peut joindre ton API de dev.
  Utiliser plutôt l'émulateur Android (`10.0.2.2`), un partage de connexion depuis
  le téléphone (réseau classé « Privé »), ou un tunnel.

Supprimer la règle quand elle ne sert plus :
`Remove-NetFirewallRule -DisplayName "API Leftly (dev)"`.

## Vérifications

```bash
npm test               # tests Jest (client API, dates, règles du foyer et de l'inventaire)
npm run typecheck      # TypeScript strict
npx expo-doctor        # compatibilité des dépendances avec le SDK
```

Les tests sont dans des dossiers `__tests__/`, **jamais dans `src/app/`** : Expo Router
considère chaque fichier de ce dossier comme un écran. Chaque fichier de test commence par
`/// <reference types="jest" />` (TypeScript 6 ne charge plus les `@types` automatiquement).

## Scan des codes-barres

- `expo-camera` lit les codes EAN-13, EAN-8 et UPC-A ; la clé de contrôle est vérifiée avant
  toute recherche (un code mal lu est ignoré).
- Seule la permission caméra est demandée (micro désactivé dans `app.config.ts`). Dans Expo Go,
  le message de demande est celui d'Expo Go ; le nôtre s'affiche dans un build de l'app.
- La recherche passe par l'API (jamais directement vers Open Food Facts). L'image du produit
  n'est affichée que sur l'écran de confirmation du scan.

## Recettes par IA

- La génération passe par l'API (aucune clé dans l'app) avec un délai de 60 s et un écran d'attente.
- En développement, l'API utilise un générateur factice (gratuit). Pour tester le vrai modèle :
  ranger la clé (voir `api/README.md`), puis lancer avec `$env:Ai__Provider = "Claude"; .dev.cmd`.
- Le bouton 🤖 (visible seulement en build de développement, `__DEV__`) copie le prompt exact
  dans le presse-papiers, pour le mettre au point dans une console d'IA sans consommer de quota.

## Rappels de péremption

- Notifications **locales** (`expo-notifications`) : le téléphone programme un résumé à 18 h
  pour les jours où des produits arrivent à leur date. Rien ne passe par le serveur.
- Elles marchent dans Expo Go (seul le push en a été retiré depuis le SDK 53). L'avertissement
  « expo-notifications functionality is not fully supported in Expo Go » au démarrage concerne
  le push : on peut l'ignorer.
- La planification est une fonction pure (`features/notifications/planner.ts`), testée avec Jest ;
  la synchronisation (`reminders.ts`) annule puis reprogramme, dans une file d'attente.
- Le bouton 🔔 du Frigo (visible seulement en build de développement, `__DEV__`) envoie le vrai
  prochain résumé 10 secondes plus tard, pour tester sans attendre 18 h.
- Le plugin `expo-notifications` n'est pas déclaré dans `app.config.ts` : il ajouterait
  l'autorisation push iOS, inutile avant la V2.

## Sessions et sécurité

- L'**access token** (15 min) reste en mémoire uniquement.
- Le **refresh token** est stocké chiffré par le système (Keychain iOS / Keystore Android)
  via `expo-secure-store`, jamais dans `AsyncStorage`.
- Sur une réponse 401, le client échange le refresh token et rejoue la requête. Un seul
  échange à la fois : l'API coupe toutes les sessions si un même refresh token est réutilisé.
- `EXPO_PUBLIC_API_URL` est intégrée en clair dans l'app : aucun secret dans les variables
  `EXPO_PUBLIC_*`.

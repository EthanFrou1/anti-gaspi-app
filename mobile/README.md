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
    ├── api/             → client API : SEUL module qui appelle le serveur
    ├── auth/            → contexte de session, stockage sécurisé des jetons
    ├── components/      → composants d'interface réutilisables
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
  New-NetFirewallRule -DisplayName "API anti-gaspi (dev)" -Direction Inbound `
    -Protocol TCP -LocalPort 5122 -Action Allow -Profile Private
  ```

- **Sur un réseau public (bureau, école…)** : ne pas ouvrir le port sur le profil
  « Public », sinon n'importe qui sur ce réseau peut joindre ton API de dev.
  Utiliser plutôt l'émulateur Android (`10.0.2.2`), un partage de connexion depuis
  le téléphone (réseau classé « Privé »), ou un tunnel.

Supprimer la règle quand elle ne sert plus :
`Remove-NetFirewallRule -DisplayName "API anti-gaspi (dev)"`.

## Vérifications

```bash
npm run typecheck      # TypeScript strict
npx expo-doctor        # compatibilité des dépendances avec le SDK
```

## Sessions et sécurité

- L'**access token** (15 min) reste en mémoire uniquement.
- Le **refresh token** est stocké chiffré par le système (Keychain iOS / Keystore Android)
  via `expo-secure-store`, jamais dans `AsyncStorage`.
- Sur une réponse 401, le client échange le refresh token et rejoue la requête. Un seul
  échange à la fois : l'API coupe toutes les sessions si un même refresh token est réutilisé.
- `EXPO_PUBLIC_API_URL` est intégrée en clair dans l'app : aucun secret dans les variables
  `EXPO_PUBLIC_*`.

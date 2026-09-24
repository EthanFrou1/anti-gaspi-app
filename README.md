# Leftly

Application mobile anti-gaspillage alimentaire : inventaire du frigo partagé par foyer,
alertes avant péremption et recettes qui utilisent en priorité ce qui périme en premier.

| Dossier | Contenu | Documentation |
|---|---|---|
| [api/](api/) | API REST ASP.NET Core (.NET 10), EF Core, PostgreSQL | [api/README.md](api/README.md) |
| [mobile/](mobile/) | App Expo (React Native, TypeScript) | [mobile/README.md](mobile/README.md) |
| [scripts/](scripts/) | Scripts de développement | ci-dessous |

## Lancer l'app en local (Windows) — en une commande

Prérequis à installer une seule fois : [Docker Desktop](https://www.docker.com/products/docker-desktop/),
le SDK .NET 10 (`winget install Microsoft.DotNet.SDK.10`), Node.js LTS
(`winget install OpenJS.NodeJS.LTS`), et l'app **Expo Go** sur le téléphone.

Depuis la racine du repo (`C:\Users\...\anti-gaspi-app`), dans un terminal :

```powershell
.\dev.cmd
```

Le script ([scripts/dev.ps1](scripts/dev.ps1)) :

1. vérifie les outils et démarre Docker Desktop si besoin ;
2. au premier lancement, crée la configuration locale : `.env` (mot de passe PostgreSQL
   aléatoire), user-secrets de l'API (chaîne de connexion, clé JWT) et `mobile/.env.local` ;
3. met à jour l'URL de l'API dans `mobile/.env.local` avec l'IP actuelle du PC ;
4. démarre PostgreSQL et applique les migrations ;
5. ouvre l'API dans une **nouvelle fenêtre** et attend qu'elle réponde ;
6. installe les dépendances mobiles, et les réinstalle quand `mobile/package-lock.json` a changé
   (dépendance ajoutée, `git pull`…) ;
7. lance Expo dans la fenêtre courante : **scanne le QR code** avec l'appareil photo
   (iPhone) ou Expo Go (Android).

Rien n'est écrasé : les fichiers et secrets déjà présents sont conservés. Aucun secret
n'est affiché et le pare-feu Windows n'est jamais modifié.

| Commande | Effet |
|---|---|
| `.\dev.cmd` | Tout démarrer pour un **téléphone réel** (même Wi-Fi que le PC) |
| `.\dev.cmd -Target emulator` | Tout démarrer pour l'**émulateur Android** |
| `.\dev.cmd -Test` | Lancer **tous les tests** (API + mobile) et le typecheck |
| `.\dev.cmd -Stop` | Arrêter l'API et PostgreSQL (les données sont conservées) |

Pour arrêter Expo : `Ctrl+C` dans sa fenêtre. En cas de souci réseau (téléphone qui ne
joint pas l'API, compte Expo), voir [mobile/README.md → Dépannage](mobile/README.md#dépannage).

## Équivalent manuel (étape par étape)

Utile pour comprendre ce que fait le script, ou si tu n'es pas sous Windows.

```powershell
# 1. Base de données — depuis la racine du repo
Copy-Item .env.example .env          # première fois : choisir un mot de passe
docker compose up -d --wait

# 2. API — depuis api/
cd api
dotnet tool restore
# première fois uniquement :
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=antigaspi;Username=antigaspi;Password=<mot de passe du .env>" --project src/Api
dotnet user-secrets set "Jwt:SigningKey" "<au moins 32 caractères aléatoires>" --project src/Api
dotnet ef database update --project src/Api
dotnet run --project src/Api --urls http://0.0.0.0:5122     # laisser ce terminal ouvert

# 3. Mobile — dans un second terminal, depuis mobile/
cd mobile
npm install
Copy-Item .env.example .env.local    # puis mettre l'IP du PC : EXPO_PUBLIC_API_URL=http://<IP>:5122
npx expo start
```

Vérifier que l'API répond : <http://localhost:5122/openapi/v1.json>.
Requêtes de test prêtes à l'emploi : [api/src/Api/Api.http](api/src/Api/Api.http).

## Tests

```powershell
.\dev.cmd -Test
```

Ou séparément : `dotnet test` dans `api/` (Docker doit tourner) et `npm test` dans `mobile/`.

## Pour aller plus loin

Le contexte du projet, les règles de sécurité et les conventions sont décrits dans
[CLAUDE.md](CLAUDE.md).

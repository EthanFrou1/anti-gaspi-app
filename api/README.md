# API

API REST ASP.NET Core (.NET 10) + Entity Framework Core + PostgreSQL.

## Structure

```
api/
├── Api.slnx
├── global.json            → épingle le SDK .NET 10
├── dotnet-tools.json      → outil local dotnet-ef (migrations)
├── src/Api/
│   ├── Controllers/       → endpoints HTTP (pas de logique métier)
│   ├── Services/          → logique métier
│   ├── Data/              → DbContext, configurations EF, migrations
│   ├── Entities/          → entités persistées (jamais exposées directement)
│   └── Dtos/              → objets d'entrée/sortie de l'API
└── tests/Api.Tests/       → tests unitaires (xUnit)
```

## Lancer en local

Prérequis : SDK .NET 10, Docker Desktop.

```bash
# 1. Depuis la racine du repo : créer le .env puis démarrer PostgreSQL
cp .env.example .env            # puis choisir un vrai mot de passe
docker compose up -d --wait

# 2. Depuis api/ : restaurer l'outil EF et stocker la chaîne de connexion
#    dans les user-secrets (hors du repo)
dotnet tool restore
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=localhost;Port=5432;Database=antigaspi;Username=antigaspi;Password=<mot de passe du .env>" \
  --project src/Api
# Clé de signature des JWT (au moins 32 caractères aléatoires)
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 64 | tr -d '\n')" --project src/Api

# 3. Appliquer les migrations et lancer l'API
dotnet ef database update --project src/Api
dotnet run --project src/Api
```

## Tests

```bash
dotnet test
```

Les tests qui touchent la base utilisent **Testcontainers** : un conteneur `postgres:18-alpine`
est démarré automatiquement (Docker Desktop doit tourner), partagé par toute la série,
et la base est vidée entre chaque test.

Pour tester l'API à la main : [src/Api/Api.http](src/Api/Api.http).

## Migrations

```bash
dotnet ef migrations add <NomDeLaMigration> --project src/Api --output-dir Data/Migrations
dotnet ef database update --project src/Api
```

## Configuration

| Clé | Où | Rôle |
|---|---|---|
| `ConnectionStrings:Default` | user-secrets (dev), variable d'env `ConnectionStrings__Default` (prod) | Connexion PostgreSQL |
| `Jwt:SigningKey` | user-secrets (dev), variable d'env `Jwt__SigningKey` (prod) | Clé secrète de signature des JWT |
| `Jwt:Issuer`, `Jwt:Audience`, durées | `appsettings.json` | Paramètres non secrets des jetons |
| `OpenFoodFacts:ContactEmail` | user-secrets (dev), variable d'env `OpenFoodFacts__ContactEmail` (prod) | Contact inclus dans le User-Agent, exigé par Open Food Facts (hors repo : le dépôt est public) |
| `OpenFoodFacts:BaseUrl` | `appsettings.json` | URL de l'API Open Food Facts |
| `App:Name` | `appsettings.json` | Nom de l'application (provisoire, centralisé ici) |

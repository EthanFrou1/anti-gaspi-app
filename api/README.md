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

# 3. Appliquer les migrations et lancer l'API
dotnet ef database update --project src/Api
dotnet run --project src/Api
```

## Migrations

```bash
dotnet ef migrations add <NomDeLaMigration> --project src/Api --output-dir Data/Migrations
dotnet ef database update --project src/Api
```

## Configuration

| Clé | Où | Rôle |
|---|---|---|
| `ConnectionStrings:Default` | user-secrets (dev), variable d'env `ConnectionStrings__Default` (prod) | Connexion PostgreSQL |
| `App:Name` | `appsettings.json` | Nom de l'application (provisoire, centralisé ici) |

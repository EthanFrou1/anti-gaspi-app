# Image de production de l'API (déploiement préparé, mis en place plus tard : voir CLAUDE.md).
# Construction depuis le dossier api/ (workflow GitHub Actions prévu plus tard) :
#   docker build -t leftly-api ./api

# ---------- Étape 1 : compilation (SDK complet, jeté à la fin) ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Le .csproj seul d'abord : tant qu'il ne change pas, Docker réutilise la couche
# « restore » en cache, même si le code change.
# (global.json n'est pas copié : la version du SDK est fixée par l'image ci-dessus.)
COPY src/Api/Api.csproj src/Api/
RUN dotnet restore src/Api/Api.csproj

COPY src/Api/ src/Api/
RUN dotnet publish src/Api/Api.csproj --configuration Release --no-restore --output /app /p:UseAppHost=false

# ---------- Étape 2 : exécution (runtime ASP.NET seul, Alpine) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final

# - icu-libs : cultures et comparaisons de texte (les images Alpine de .NET sont par défaut
#   en mode « invariant », sans ICU) ;
# - tzdata : fuseaux horaires (Ai:TimeZone = Europe/Paris pour les quotas de recettes ;
#   l'API refuse de démarrer si le fuseau est introuvable).
# wget est déjà présent (busybox) : Coolify s'en sert pour la vérification de santé.
RUN apk add --no-cache icu-libs tzdata

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_HTTP_PORTS=8080

WORKDIR /app
# Fichiers appartenant à root : l'utilisateur de l'app peut les lire, pas les modifier.
COPY --from=build /app ./

# Utilisateur non root fourni par les images .NET (UID 1654) : une faille dans l'API
# ne donnerait pas les droits root dans le conteneur. Port 8080 (pas besoin de root).
USER $APP_UID
EXPOSE 8080

# Même vérification que celle configurée dans Coolify : l'API répond et joint sa base.
# start-period : laisse le temps d'appliquer les migrations au démarrage.
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 \
    CMD wget -q -O /dev/null http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "Api.dll"]

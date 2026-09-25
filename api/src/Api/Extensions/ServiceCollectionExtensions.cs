using Api.Authorization;
using Api.Common;
using Api.Data;
using Api.Entities;
using Api.Options;
using Api.Services.Ai;
using Api.Services.Auth;
using Api.Services.Households;
using Api.Services.Inventory;
using Api.Services.Products;
using Api.Services.Profiles;
using Api.Services.Receipts;
using Api.Services.Recipes;
using Anthropic;
using Api.Services.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Api.Extensions;

/// <summary>
/// Enregistrement des services. Regroupé ici pour garder Program.cs lisible
/// et pour que les tests utilisent exactement la même configuration.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppIdentity(this IServiceCollection services)
    {
        // AddIdentityCore (et non AddIdentity) : uniquement la gestion des comptes,
        // sans l'authentification par cookies dont une API mobile n'a pas besoin.
        services.AddIdentityCore<User>(options =>
            {
                // Recommandation actuelle (NIST) : privilégier la longueur plutôt
                // que des règles de composition (majuscule, symbole…) peu efficaces.
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = true;
                // Le UserName contient l'email : on n'en restreint pas les caractères.
                options.User.AllowedUserNameCharacters = string.Empty;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddErrorDescriber<FrenchIdentityErrorDescriber>();

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        // ValidateOnStart : l'API refuse de démarrer si la config JWT est absente ou invalide,
        // plutôt que d'échouer à la première connexion.
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                // Garde les noms de claims standard (« sub ») au lieu des URI longues de .NET.
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = JwtSigningKey.Create(jwt.SigningKey),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    // N'accepte que notre algorithme : bloque les attaques qui changent
                    // l'en-tête « alg » du jeton (ex. « none »).
                    ValidAlgorithms = [JwtSigningKey.Algorithm],
                    // Tolérance sur les horloges (5 min par défaut, trop large pour un jeton de 15 min).
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        return services;
    }

    public static IServiceCollection AddAppAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // Sécurisé par défaut : tout endpoint exige un utilisateur connecté,
            // sauf ceux marqués explicitement [AllowAnonymous]. Un oubli ne peut donc
            // pas exposer un endpoint par accident.
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            options.AddPolicy(HouseholdPolicies.Member, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new HouseholdAccessRequirement(ownerOnly: false)));

            options.AddPolicy(HouseholdPolicies.Owner, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new HouseholdAccessRequirement(ownerOnly: true)));
        });

        // Scoped : le handler interroge la base via le DbContext de la requête.
        services.AddScoped<IAuthorizationHandler, HouseholdAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, HouseholdAuthorizationResultHandler>();

        return services;
    }

    public static IServiceCollection AddOpenFoodFacts(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpenFoodFactsOptions>()
            .Bind(configuration.GetSection(OpenFoodFactsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddMemoryCache();
        services.AddSingleton<OpenFoodFactsRateLimiter>();

        // Client HTTP « typé » : IHttpClientFactory gère la réutilisation des connexions
        // (créer un HttpClient par requête épuiserait les sockets).
        services.AddHttpClient<IOpenFoodFactsClient, OpenFoodFactsClient>((provider, http) =>
        {
            var options = provider.GetRequiredService<IOptions<OpenFoodFactsOptions>>().Value;
            var appName = configuration["App:Name"] ?? "App";

            http.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            // Délai court : si OFF est lent, l'utilisateur bascule vite sur la saisie manuelle.
            http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            // Une fiche produit pèse quelques Ko : on refuse les réponses anormalement grosses.
            http.MaxResponseContentBufferSize = 1_000_000;
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                OpenFoodFactsUserAgent.Build(appName, options.ContactEmail));
        });

        services.AddScoped<IProductLookupService, ProductLookupService>();
        return services;
    }

    /// <summary>
    /// Fonctionnalités d'IA (recettes, lecture des tickets) : choisit les implémentations
    /// selon Ai:Provider, après le garde-fou (Fake interdit hors Development, clé obligatoire
    /// en production). Une configuration invalide lève une exception : l'API ne démarre pas.
    /// </summary>
    public static IServiceCollection AddAiFeatures(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var apiKey = configuration[$"{AnthropicOptions.SectionName}:{nameof(AnthropicOptions.ApiKey)}"];
        var provider = AiConfigurationGuard.Validate(
            configuration[$"{AiOptions.SectionName}:{nameof(AiOptions.Provider)}"] ?? nameof(AiProvider.Claude),
            apiKey,
            environment.IsDevelopment());

        services.AddOptions<AiOptions>()
            .Bind(configuration.GetSection(AiOptions.SectionName))
            // Quota de tickets relevé (script d'évaluation) : seulement en Development.
            .PostConfigure(ai => ai.ApplyEnvironment(environment.IsDevelopment()))
            .ValidateDataAnnotations()
            // Fuseau introuvable (ex. paquet tzdata absent de l'image Docker) : l'API refuse de
            // démarrer, plutôt que de planter au premier calcul de quota.
            .Validate(
                ai => TimeZoneInfo.TryFindSystemTimeZoneById(ai.TimeZone, out _),
                "Ai:TimeZone : fuseau horaire introuvable (tzdata est-il installé ?).")
            .ValidateOnStart();

        services.AddHostedService<AiCleanupService>();
        // Compteur des tokens consommés depuis le démarrage (lu par un outil de développement).
        services.AddSingleton<AiUsageMeter>();

        if (provider == AiProvider.Fake)
        {
            services.AddSingleton<IRecipeGenerator, FakeRecipeGenerator>();
            services.AddSingleton<IReceiptReader, FakeReceiptReader>();
            return services;
        }

        // Client Anthropic unique (réutilise ses connexions HTTP). Délai et nouvelles tentatives
        // bornés pour rester sous le délai de 60 s de l'app mobile.
        services.AddSingleton(provider2 =>
        {
            var ai = provider2.GetRequiredService<IOptions<AiOptions>>().Value;
            return new AnthropicClient
            {
                ApiKey = apiKey ?? string.Empty,
                Timeout = TimeSpan.FromSeconds(ai.TimeoutSeconds),
                MaxRetries = ai.MaxRetries,
            };
        });
        services.AddSingleton<IRecipeGenerator>(provider2 => string.IsNullOrWhiteSpace(apiKey)
            // Développement sans clé : l'API démarre, la génération répond « IA non configurée ».
            ? new UnconfiguredRecipeGenerator()
            : ActivatorUtilities.CreateInstance<ClaudeRecipeGenerator>(provider2));
        services.AddSingleton<IReceiptReader>(provider2 => string.IsNullOrWhiteSpace(apiKey)
            ? new UnconfiguredReceiptReader()
            : ActivatorUtilities.CreateInstance<ClaudeReceiptReader>(provider2));
        return services;
    }

    /// <summary>
    /// Derrière le proxy de Coolify, l'API voit l'IP du proxy pour toutes les requêtes, en HTTP.
    /// Le proxy transmet la vraie IP du client (X-Forwarded-For) et le protocole d'origine
    /// (X-Forwarded-Proto) : on ne les croit que s'ils viennent d'un réseau déclaré.
    /// </summary>
    public static IServiceCollection AddReverseProxySupport(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ReverseProxyOptions>()
            .Bind(configuration.GetSection(ReverseProxyOptions.SectionName))
            .Validate(ReverseProxyOptions.AreValid, "ReverseProxy:KnownNetworks : notation CIDR attendue (ex. 10.0.1.0/24).")
            .ValidateOnStart();

        services.AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<ReverseProxyOptions>>((forwarded, proxy) =>
            {
                forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                // Un seul proxy : on ne lit que la dernière adresse ajoutée à X-Forwarded-For,
                // celle que Traefik a vue. Les adresses ajoutées avant par le client sont ignorées.
                forwarded.ForwardLimit = 1;
                foreach (var network in proxy.Value.KnownNetworks)
                {
                    forwarded.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
                }
            });

        return services;
    }

    /// <summary>Endpoint /health : l'API répond et joint sa base (voir DatabaseHealthCheck).</summary>
    public static IServiceCollection AddAppHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // TimeProvider injecté plutôt que DateTimeOffset.UtcNow : les tests peuvent
        // ainsi avancer le temps (ex. vérifier l'expiration d'un jeton).
        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IHouseholdAccessService, HouseholdAccessService>();
        services.AddScoped<IHouseholdService, HouseholdService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IRecipeService, RecipeService>();
        services.AddScoped<IReceiptService, ReceiptService>();

        return services;
    }
}

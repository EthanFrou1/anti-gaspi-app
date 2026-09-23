using Api.Authorization;
using Api.Data;
using Api.Entities;
using Api.Options;
using Api.Services.Auth;
using Api.Services.Households;
using Api.Services.Inventory;
using Api.Services.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
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

        return services;
    }
}

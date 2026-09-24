using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Api.Common;
using Api.Data;
using Api.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

var builder = WebApplication.CreateBuilder(args);

// Base de données : la chaîne de connexion vient des user-secrets (dev)
// ou d'une variable d'environnement (prod), jamais d'un fichier versionné.
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Chaîne de connexion 'ConnectionStrings:Default' manquante (voir api/README.md).");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

builder.Services
    .AddAppIdentity()
    .AddJwtAuthentication(builder.Configuration)
    .AddAppAuthorization()
    .AddApplicationServices()
    .AddOpenFoodFacts(builder.Configuration)
    .AddRecipeGeneration(builder.Configuration, builder.Environment)
    .AddReverseProxySupport(builder.Configuration)
    .AddAppHealthChecks();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // 30 requêtes par minute et par adresse IP sur les endpoints d'authentification.
    // Limite volontairement large (plusieurs utilisateurs peuvent partager une IP, ex.
    // Wi-Fi de résidence) : la force brute sur un compte est surtout freinée par le
    // verrouillage par compte d'Identity.
    options.AddPolicy(RateLimitPolicies.Auth, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Recherche par code-barres : 30 par minute et par utilisateur. Le cache absorbe les
    // codes déjà vus ; cette limite protège le quota d'Open Food Facts (100 req/min au total).
    options.AddPolicy(RateLimitPolicies.ProductLookup, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Entrée dans un foyer : 10 essais de code par minute et par utilisateur.
    options.AddPolicy(RateLimitPolicies.JoinHousehold, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

builder.Services.AddControllers(options =>
    {
        // Outils de mise au point (ex. aperçu du prompt) : absents hors Development.
        if (!builder.Environment.IsDevelopment())
        {
            options.Conventions.Add(new RemoveDevelopmentOnlyActionsConvention());
        }
    })
    // Enums en texte dans le JSON (« Owner » plutôt que 1) : plus lisible et stable côté mobile.
    // allowIntegerValues: false : seuls les noms sont acceptés (« Vegan »). Sinon, « 99 »
    // serait converti en une valeur d'énumération inexistante.
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddProblemDetails(options =>
{
    // Remplace le titre anglais par défaut des erreurs de validation
    // (« One or more validation errors occurred. »).
    options.CustomizeProblemDetails = context =>
    {
        if (context.ProblemDetails is HttpValidationProblemDetails)
        {
            context.ProblemDetails.Title = "Les informations saisies sont invalides.";
        }
    };
});
builder.Services.AddOpenApi();

var app = builder.Build();

// EN PREMIER : tout ce qui suit (limite de débit par IP, journaux) doit voir la vraie IP
// du client et le vrai protocole, pas ceux du proxy de Coolify.
app.UseForwardedHeaders();

// Les erreurs non gérées (500) sont renvoyées au format ProblemDetails, sans détail interne.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

// Hors développement, le HTTPS est géré par le proxy (Traefik redirige HTTP vers HTTPS).
// On ne redirige pas dans l'API : une requête POST redirigée perdrait son corps, et la
// vérification de santé de Coolify, en HTTP dans le conteneur, serait redirigée elle aussi.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

// Vérification de santé pour Coolify : anonyme (fallback « connecté » sinon), sans détail.
app.MapHealthChecks("/health").AllowAnonymous();

if (!app.Environment.IsDevelopment())
{
    // Les outils de développement n'existent pas ailleurs : 404 franc, quelle que soit la méthode.
    DevelopmentOnlyEndpoints.MapNotFoundStubs(app);
}

// Production : migrations appliquées avant d'ouvrir le port (Database:MigrateOnStartup).
await app.MigrateDatabaseIfEnabledAsync();

app.Run();

// Rend la classe Program (générée par les instructions de haut niveau) visible
// des tests de bout en bout, qui démarrent l'API en mémoire avec WebApplicationFactory<Program>.
public partial class Program;

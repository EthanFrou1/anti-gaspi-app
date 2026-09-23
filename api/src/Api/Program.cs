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
    .AddApplicationServices();

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

builder.Services.AddControllers()
    // Enums en texte dans le JSON (« Owner » plutôt que 1) : plus lisible et stable côté mobile.
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
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

// Les erreurs non gérées (500) sont renvoyées au format ProblemDetails, sans détail interne.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.Run();

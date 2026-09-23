using System.Threading.RateLimiting;
using Api.Common;
using Api.Data;
using Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

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
    .AddApplicationServices();

builder.Services.AddAuthorization(options =>
{
    // Sécurisé par défaut : tout endpoint exige un utilisateur connecté,
    // sauf ceux marqués explicitement [AllowAnonymous]. Un oubli ne peut donc
    // pas exposer un endpoint par accident.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

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
});

builder.Services.AddControllers();
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

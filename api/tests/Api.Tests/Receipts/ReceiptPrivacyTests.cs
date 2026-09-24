using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using Api.Dtos.Auth;
using Api.Dtos.Households;
using Api.Entities;
using Api.Services.Auth;
using Api.Services.Households;
using Api.Services.Receipts;
using Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Api.Tests.Receipts;

/// <summary>
/// Règle du projet : la photo d'un ticket n'est JAMAIS conservée (ni disque, ni base, ni journaux),
/// y compris en cas d'erreur. Tests de bout en bout, à travers la vraie API.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ReceiptPrivacyTests(DatabaseFixture database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // ---------- Disque ----------

    [Theory]
    [InlineData(1_500_000, HttpStatusCode.OK)]                               // bien au-delà du seuil par défaut de 64 Ko
    [InlineData(ReceiptImageFormat.MaxBytes + 200_000, HttpStatusCode.BadRequest)] // trop gros : refusé
    public async Task TheImage_IsNeverBufferedToDisk(int size, HttpStatusCode expected)
    {
        // Par défaut, ASP.NET Core écrit un fichier envoyé de plus de 64 Ko dans un fichier
        // temporaire « ASPNETCORE_*.tmp ». On surveille le dossier pendant le scan.
        var tempDirectory = Environment.GetEnvironmentVariable("ASPNETCORE_TEMP") is { Length: > 0 } custom
            ? custom
            : Path.GetTempPath();
        var created = new ConcurrentBag<string>();
        using var watcher = new FileSystemWatcher(tempDirectory, "ASPNETCORE_*.tmp") { EnableRaisingEvents = true };
        watcher.Created += (_, e) => created.Add(e.Name ?? "?");

        var (client, householdId) = await SetupAsync(database.Api);
        var response = await client.PostAsync(ScanUrl(householdId), ImageForm(TestJpeg.OfSize(size)));
        // Les événements du système de fichiers arrivent de façon asynchrone.
        await Task.Delay(500);

        Assert.True(response.StatusCode == expected || (expected == HttpStatusCode.BadRequest && response.StatusCode == HttpStatusCode.RequestEntityTooLarge),
            $"Statut inattendu : {response.StatusCode}");
        Assert.Empty(created);
    }

    // ---------- Base de données ----------

    [Fact]
    public void TheScanTable_HasNoRoomForTheImage()
    {
        // Seules colonnes de la seule table liée aux tickets : si quelqu'un y ajoute une colonne
        // (image, texte lu, magasin…), ce test échoue et oblige à revoir la règle.
        using var db = database.CreateDbContext();
        var columns = db.Model.FindEntityType(typeof(ReceiptScan))!.GetProperties().Select(p => p.Name).Order();

        Assert.Equal(["CreatedAt", "Id", "IsCompleted", "UserId"], columns);
    }

    [Fact]
    public async Task AfterASuccessfulScan_TheDatabaseHoldsOnlyTheQuotaEntry()
    {
        var (client, householdId) = await SetupAsync(database.Api);

        await client.PostAsync(ScanUrl(householdId), ImageForm(TestJpeg.PhotoWithMetadata));

        await using var db = database.CreateDbContext();
        Assert.Single(await db.ReceiptScans.ToListAsync());
        Assert.Empty(await db.InventoryItems.ToListAsync());
    }

    // ---------- Journaux ----------

    public enum Outcome
    {
        Success,
        AiUnavailable,
        UnexpectedError,
    }

    [Theory]
    [InlineData(Outcome.Success, HttpStatusCode.OK)]
    [InlineData(Outcome.AiUnavailable, HttpStatusCode.ServiceUnavailable)]
    [InlineData(Outcome.UnexpectedError, HttpStatusCode.InternalServerError)]
    public async Task TheImage_NeverAppearsInTheLogs(Outcome outcome, HttpStatusCode expected)
    {
        var logs = new CapturingLoggerProvider();
        var reader = new ThrowingReader(outcome);
        await using var api = database.Api.WithWebHostBuilder(builder =>
        {
            // Tous les niveaux, y compris Trace, pour toutes les catégories (ASP.NET Core compris).
            builder.ConfigureLogging(logging => logging.AddProvider(logs).AddFilter<CapturingLoggerProvider>(null, LogLevel.Trace));
            builder.ConfigureTestServices(services => services.AddSingleton<IReceiptReader>(reader));
        });
        var (client, householdId) = await SetupAsync(api);
        var photo = TestJpeg.PhotoWithMetadata;

        var response = await client.PostAsync(ScanUrl(householdId), ImageForm(photo));

        Assert.Equal(expected, response.StatusCode);
        Assert.NotEmpty(logs.Entries); // la capture fonctionne
        var text = logs.AllText;
        Assert.DoesNotContain(Convert.ToBase64String(photo), text);
        Assert.DoesNotContain(Convert.ToBase64String(JpegMetadataStripper.Strip(photo)!), text);
        // Ni les métadonnées de la photo, ni un extrait de ses octets.
        Assert.DoesNotContain("iPhone 15", text);
        Assert.DoesNotContain(Convert.ToBase64String(photo[100..160]), text);
    }

    /// <summary>Lecteur qui réussit, ou échoue comme demandé, après avoir reçu l'image.</summary>
    private sealed class ThrowingReader(Outcome outcome) : IReceiptReader
    {
        public Task<ReceiptDraft> ReadAsync(ReceiptImage image, ReceiptPrompt prompt, CancellationToken ct) => outcome switch
        {
            Outcome.AiUnavailable => throw new Api.Services.Ai.AiUnavailableException("panne simulée"),
            Outcome.UnexpectedError => throw new InvalidOperationException("erreur inattendue simulée"),
            _ => new FakeReceiptReader().ReadAsync(image, prompt, ct),
        };
    }

    // ---------- Helpers ----------

    private static string ScanUrl(Guid householdId) => $"/api/households/{householdId}/receipts/scan";

    private static MultipartFormDataContent ImageForm(byte[] image)
    {
        var file = new ByteArrayContent(image);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return new MultipartFormDataContent { { file, "image", "ticket.jpg" } };
    }

    private static async Task<(HttpClient Client, Guid HouseholdId)> SetupAsync(WebApplicationFactory<Program> api)
    {
        using var scope = api.Services.CreateScope();
        var services = scope.ServiceProvider;
        var user = await services.GetRequiredService<IAuthService>().RegisterAsync(
            new RegisterRequest($"privacy-{Guid.NewGuid():N}@example.com", DatabaseTestBase.DefaultPassword, "Coloc"),
            CancellationToken.None);
        var household = await services.GetRequiredService<IHouseholdService>()
            .CreateAsync(user.Value!.User.Id, new CreateHouseholdRequest("Coloc"), CancellationToken.None);

        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Value.AccessToken);
        return (client, household.Value!.Id);
    }
}

using Api.Common;
using Api.Data;
using Api.Data.Seed;
using Api.Dtos.Receipts;
using Api.Entities;
using Api.Options;
using Api.Services.Ai;
using Api.Services.Inventory;
using Api.Services.Receipts;
using Api.Tests.Households;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests.Receipts;

public class ReceiptServiceTests(DatabaseFixture database) : HouseholdTestBase(database)
{
    private static readonly TimeZoneInfo Paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

    // Vraie photo JPEG, avec métadonnées (EXIF, GPS…).
    internal static readonly byte[] Jpeg = TestJpeg.PhotoWithMetadata;

    private DateOnly Today => QuotaDay.Window(Clock.Now, Paris).LocalDate;

    // ---------- Lecture ----------

    [Fact]
    public async Task Scan_ReturnsTheFoodLines_WithEstimatedExpiryDates()
    {
        var alice = await CreateUserAsync("Alice");

        var result = await ScanAsync(alice);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var scan = result.Value;
        // Le Fake ne donne pas de date : aujourd'hui (heure de Paris).
        Assert.Equal(Today, scan.PurchasedOn);
        Assert.False(scan.PurchaseDateFromReceipt);
        Assert.Equal(5, scan.Lines.Count);
        Assert.Equal(1, scan.SkippedLineCount); // la lessive
        var steak = scan.Lines.Single(l => l.Name.StartsWith("Steak", StringComparison.Ordinal));
        Assert.Equal(CategorySeed.Categories.Single(c => c.Code == "ground-meat").Id, steak.CategoryId);
        Assert.Equal(Today.AddDays(1), steak.ExpiresOn);
        Assert.Equal(ExpiryKind.UseBy, steak.ExpiryKind);
    }

    [Fact]
    public async Task Scan_SendsTheImageWithoutItsMetadata_AndStoresNoContent()
    {
        var alice = await CreateUserAsync("Alice");

        await ScanAsync(alice);

        // Ce qui partirait vers l'IA : la photo, sans EXIF ni position GPS.
        Assert.Equal("image/jpeg", ReceiptReader.LastImage!.MediaType);
        Assert.Equal(JpegMetadataStripper.Strip(Jpeg), ReceiptReader.LastImage.Data);
        Assert.False(TestJpeg.Contains(ReceiptReader.LastImage.Data, "Exif"));
        // Seule trace en base : l'utilisateur et l'heure, pour les quotas.
        await using var db = CreateDbContext();
        var scan = await db.ReceiptScans.SingleAsync();
        Assert.Equal(alice, scan.UserId);
        Assert.True(scan.IsCompleted);
        Assert.Empty(await db.InventoryItems.ToListAsync());
    }

    [Theory]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 })]   // GIF
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46 })]               // PDF
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A })]   // PNG
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 })]   // début de JPEG, sans image derrière
    [InlineData(new byte[0])]
    public async Task UnsupportedImage_IsRejected_WithoutCallingTheAi_NorUsingTheQuota(byte[] image)
    {
        var alice = await CreateUserAsync("Alice");

        var result = await ScanAsync(alice, image);

        Assert.Equal(ReceiptErrors.InvalidImage, result.Error);
        Assert.Equal(0, ReceiptReader.Calls);
        Assert.Equal(0, (await QuotaAsync(alice)).Used);
    }

    [Fact]
    public async Task TooLargeImage_IsRejected()
    {
        var alice = await CreateUserAsync("Alice");
        var image = new byte[ReceiptImageFormat.MaxBytes + 1];
        Jpeg.CopyTo(image, 0);

        var result = await ScanAsync(alice, image);

        Assert.Equal(ReceiptErrors.InvalidImage, result.Error);
        Assert.Equal(0, ReceiptReader.Calls);
    }

    // ---------- Quotas ----------

    [Fact]
    public async Task FourthScanOfTheDay_IsRejected_ThenAllowedAfterMidnight()
    {
        var alice = await CreateUserAsync("Alice");
        for (var i = 0; i < 3; i++)
        {
            Assert.True((await ScanAsync(alice)).IsSuccess);
        }

        var fourth = await ScanAsync(alice);
        Assert.Equal(ReceiptErrors.DailyQuotaReached, fourth.Error);
        Assert.Equal(3, ReceiptReader.Calls);

        var quota = await QuotaAsync(alice);
        Assert.Equal(0, quota.Remaining);
        Clock.Advance(quota.ResetsAt - Clock.Now + TimeSpan.FromMinutes(1));

        Assert.True((await ScanAsync(alice)).IsSuccess);
    }

    [Fact]
    public async Task ReceiptQuota_IsSeparateFromTheRecipeQuota()
    {
        var alice = await CreateUserAsync("Alice");
        await using (var db = CreateDbContext())
        {
            var householdId = await CreateHouseholdAsync(alice);
            db.RecipeGenerations.AddRange(Enumerable.Range(0, 3).Select(_ => new RecipeGeneration
            {
                HouseholdId = householdId, RequestedByUserId = alice, CreatedAt = Clock.Now, RecipeJson = "{}",
            }));
            await db.SaveChangesAsync();
        }

        Assert.True((await ScanAsync(alice)).IsSuccess);
    }

    [Fact]
    public async Task GlobalDailyLimit_AppliesAcrossAllUsers()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var limited = new AiOptions { ReceiptGlobalDailyLimit = 2 };

        Assert.True((await ScanAsync(alice, options: limited)).IsSuccess);
        Assert.True((await ScanAsync(bob, options: limited)).IsSuccess);

        Assert.Equal(ReceiptErrors.GlobalQuotaReached, (await ScanAsync(alice, options: limited)).Error);
    }

    [Fact]
    public async Task FailedReading_IsNotCounted()
    {
        var alice = await CreateUserAsync("Alice");
        ReceiptReader.FailWithUnavailable = true;

        var result = await ScanAsync(alice);

        Assert.Equal(ReceiptErrors.Unavailable, result.Error);
        Assert.Equal(0, (await QuotaAsync(alice)).Used);
    }

    [Fact]
    public async Task InvalidAnswer_IsUnavailable_AndNotCounted()
    {
        var alice = await CreateUserAsync("Alice");
        ReceiptReader.Override = () => new ReceiptDraft(null, Lines: null);

        var result = await ScanAsync(alice);

        Assert.Equal(ReceiptErrors.Unavailable, result.Error);
        Assert.Equal(0, (await QuotaAsync(alice)).Used);
    }

    [Fact]
    public async Task EmptyReading_IsCounted()
    {
        // Photo qui n'est pas un ticket : l'IA a répondu (et a été payée), la lecture compte.
        var alice = await CreateUserAsync("Alice");
        ReceiptReader.Override = () => new ReceiptDraft(null, []);

        var result = await ScanAsync(alice);

        Assert.Empty(result.Value!.Lines);
        Assert.Equal(1, (await QuotaAsync(alice)).Used);
    }

    [Fact]
    public async Task DeletingTheAccount_DeletesItsScans()
    {
        var alice = await CreateUserAsync("Alice");
        await ScanAsync(alice);

        await DeleteAccountAsync(alice);

        await using var db = CreateDbContext();
        Assert.Empty(await db.ReceiptScans.ToListAsync());
    }

    // ---------- Nettoyage ----------

    [Fact]
    public async Task Purge_RemovesOldScans_AndAbandonedReservations()
    {
        var alice = await CreateUserAsync("Alice");
        await ScanAsync(alice);
        await using (var db = CreateDbContext())
        {
            // Réservation restée en cours (crash pendant l'appel à l'IA).
            db.ReceiptScans.Add(new ReceiptScan { UserId = alice, CreatedAt = Clock.Now, IsCompleted = false });
            await db.SaveChangesAsync();
        }

        Clock.Advance(TimeSpan.FromMinutes(15));
        Assert.Equal(1, await PurgeAsync());   // la réservation abandonnée

        Clock.Advance(TimeSpan.FromHours(48));
        Assert.Equal(1, await PurgeAsync());   // la lecture terminée, au-delà de 48 heures
    }

    // ---------- Helpers ----------

    private async Task<Result<ReceiptScanDto>> ScanAsync(Guid userId, byte[]? image = null, AiOptions? options = null)
    {
        await using var scope = CreateScope();
        var provider = scope.ServiceProvider;
        var service = options is null
            ? provider.GetRequiredService<IReceiptService>()
            : new ReceiptService(provider.GetRequiredService<AppDbContext>(), provider.GetRequiredService<ICategoryService>(),
                ReceiptReader, Microsoft.Extensions.Options.Options.Create(options), Clock, NullLogger<ReceiptService>.Instance);
        return await service.ScanAsync(userId, image ?? Jpeg, CancellationToken.None);
    }

    private Task<ReceiptQuotaDto> QuotaAsync(Guid userId) =>
        WithServiceAsync<IReceiptService, ReceiptQuotaDto>(s => s.GetQuotaAsync(userId, CancellationToken.None));

    private Task<int> PurgeAsync() =>
        WithServiceAsync<IReceiptService, int>(s => s.PurgeAsync(CancellationToken.None));
}

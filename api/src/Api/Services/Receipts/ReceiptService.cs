using Api.Common;
using Api.Data;
using Api.Dtos.Receipts;
using Api.Entities;
using Api.Options;
using Api.Services.Ai;
using Api.Services.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services.Receipts;

public static class ReceiptErrors
{
    public static readonly Error InvalidImage = new(
        ErrorType.Validation, "receipt.invalid_image", "La photo doit être une image JPEG de moins de 2 Mo.",
        new Dictionary<string, string[]> { ["image"] = ["La photo doit être une image JPEG de moins de 2 Mo."] });

    public static readonly Error DailyQuotaReached = new(
        ErrorType.TooManyRequests, "receipt.daily_quota",
        "Tu as utilisé tous tes scans de ticket du jour. Reviens demain, ou ajoute tes produits à la main !");

    public static readonly Error GlobalQuotaReached = new(
        ErrorType.TooManyRequests, "receipt.global_quota",
        "Beaucoup de tickets ont déjà été lus aujourd'hui sur l'app. Réessaie demain, ou ajoute tes produits à la main !");

    // Message volontairement générique : la cause précise (refus, panne…) reste dans les journaux.
    public static readonly Error Unavailable = new(
        ErrorType.Unavailable, "receipt.unavailable",
        "La lecture du ticket est momentanément indisponible. Réessaie dans un instant.");
}

public interface IReceiptService
{
    /// <summary>
    /// Lit un ticket de caisse (quota : 3 par jour et par utilisateur). L'image n'est jamais
    /// enregistrée ; rien n'est ajouté au frigo (l'utilisateur valide d'abord les lignes).
    /// </summary>
    Task<Result<ReceiptScanDto>> ScanAsync(Guid actorId, byte[] image, CancellationToken ct);

    Task<ReceiptQuotaDto> GetQuotaAsync(Guid userId, CancellationToken ct);

    /// <summary>Nettoyage périodique : lectures de plus de 48 heures, réservations abandonnées.</summary>
    Task<int> PurgeAsync(CancellationToken ct);
}

public sealed class ReceiptService(
    AppDbContext db,
    ICategoryService categories,
    IReceiptReader reader,
    IOptions<AiOptions> options,
    TimeProvider time,
    ILogger<ReceiptService> logger) : IReceiptService
{
    // Seul le quota du jour compte : inutile de garder plus longtemps (minimisation).
    public static readonly TimeSpan Retention = TimeSpan.FromHours(48);

    // Une réservation non terminée après ce délai correspond à un crash pendant l'appel à l'IA.
    public static readonly TimeSpan StaleReservation = TimeSpan.FromMinutes(10);

    // Clé du verrou PostgreSQL des quotas de tickets (distincte de celle des recettes).
    private const long QuotaLockKey = 4_242_002;

    private AiOptions Settings => options.Value;

    public async Task<Result<ReceiptScanDto>> ScanAsync(Guid actorId, byte[] image, CancellationToken ct)
    {
        // Vérifié avant de réserver : une image refusée ne coûte rien et n'est pas décomptée.
        // Les métadonnées (EXIF, position GPS…) sont retirées avant tout envoi à l'IA.
        var cleaned = image.Length <= ReceiptImageFormat.MaxBytes && ReceiptImageFormat.IsJpeg(image)
            ? JpegMetadataStripper.Strip(image)
            : null;
        if (cleaned is null)
        {
            return ReceiptErrors.InvalidImage;
        }

        var categoryList = await categories.ListAsync(ct);
        var today = QuotaDay.Window(time.GetUtcNow(), Zone()).LocalDate;
        var prompt = ReceiptPromptBuilder.Build(categoryList, today);

        // 1. Réservation d'une place dans les quotas (courte transaction, sous verrou).
        var reservation = await ReserveAsync(actorId, ct);
        if (!reservation.IsSuccess)
        {
            return reservation.Error;
        }

        // 2. Appel à l'IA HORS transaction : quelques secondes sans bloquer les autres utilisateurs.
        ValidatedReceipt receipt;
        try
        {
            var draft = await reader.ReadAsync(new ReceiptImage(cleaned, ReceiptImageFormat.JpegMediaType), prompt, ct);
            receipt = ReceiptValidator.Validate(draft, categoryList, today);
        }
        catch (AiUnavailableException ex)
        {
            logger.LogWarning("Ticket non lu : {Reason}", ex.Message);
            await CancelReservationAsync(reservation.Value);
            return ReceiptErrors.Unavailable;
        }
        catch
        {
            // Toute autre erreur (y compris l'annulation) : la place réservée est libérée.
            await CancelReservationAsync(reservation.Value);
            throw;
        }

        // 3. La lecture est décomptée, même si aucun produit n'a été reconnu : l'appel a été payé.
        await db.ReceiptScans
            .Where(r => r.Id == reservation.Value)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsCompleted, true), ct);

        return new ReceiptScanDto(
            receipt.PurchasedOn,
            receipt.PurchaseDateFromReceipt,
            receipt.Lines
                .Select(l => new ReceiptLineDto(l.ReceiptText, l.Name, l.CategoryId, l.Quantity, l.Unit, l.ExpiresOn, l.ExpiryKind))
                .ToList(),
            receipt.SkippedLineCount);
    }

    public async Task<ReceiptQuotaDto> GetQuotaAsync(Guid userId, CancellationToken ct)
    {
        var (start, end, _) = QuotaDay.Window(time.GetUtcNow(), Zone());
        var used = await db.ReceiptScans.CountAsync(r => r.UserId == userId && r.CreatedAt >= start, ct);
        var limit = Settings.ReceiptDailyLimitPerUser;
        return new ReceiptQuotaDto(used, limit, Math.Max(0, limit - used), end);
    }

    public async Task<int> PurgeAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var retentionLimit = now - Retention;
        var staleLimit = now - StaleReservation;
        return await db.ReceiptScans
            .Where(r => r.CreatedAt < retentionLimit || (!r.IsCompleted && r.CreatedAt < staleLimit))
            .ExecuteDeleteAsync(ct);
    }

    // ---------- Quotas (même mécanisme que les recettes) ----------

    private Task<Result<Guid>> ReserveAsync(Guid userId, CancellationToken ct) =>
        db.InTransactionAsync(async () =>
        {
            // Verrou PostgreSQL tenu jusqu'à la fin de la transaction : deux demandes simultanées
            // sont traitées l'une après l'autre, et ne peuvent pas dépasser un quota ensemble.
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({QuotaLockKey})", ct);

            var now = time.GetUtcNow();
            var (start, _, _) = QuotaDay.Window(now, Zone());

            // Les réservations en cours comptent : une lecture lancée occupe sa place.
            var usedByUser = await db.ReceiptScans.CountAsync(r => r.UserId == userId && r.CreatedAt >= start, ct);
            if (usedByUser >= Settings.ReceiptDailyLimitPerUser)
            {
                return (Result<Guid>)ReceiptErrors.DailyQuotaReached;
            }

            var usedGlobally = await db.ReceiptScans.CountAsync(r => r.CreatedAt >= start, ct);
            if (usedGlobally >= Settings.ReceiptGlobalDailyLimit)
            {
                logger.LogWarning("Limite globale quotidienne de tickets atteinte ({Limit}).", Settings.ReceiptGlobalDailyLimit);
                return ReceiptErrors.GlobalQuotaReached;
            }

            var reservation = new ReceiptScan { UserId = userId, CreatedAt = now };
            db.ReceiptScans.Add(reservation);
            await db.SaveChangesAsync(ct);
            return reservation.Id;
        }, ct);

    private async Task CancelReservationAsync(Guid reservationId)
    {
        // CancellationToken.None : la place doit être libérée même si la requête a été annulée.
        await db.ReceiptScans
            .Where(r => r.Id == reservationId && !r.IsCompleted)
            .ExecuteDeleteAsync(CancellationToken.None);
    }

    private TimeZoneInfo Zone() => TimeZoneInfo.FindSystemTimeZoneById(Settings.TimeZone);
}

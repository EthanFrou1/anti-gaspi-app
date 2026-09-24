using Api.Common;
using Api.Data;
using Api.Dtos.Inventory;
using Api.Entities;
using Api.Services.Households;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Inventory;

/// <summary>
/// Inventaire d'un foyer. L'appartenance au foyer est vérifiée en amont par la politique
/// HouseholdMember ; ce service vérifie en plus que le produit appartient bien à CE foyer
/// (protection IDOR) et applique les droits sur les produits perso.
/// </summary>
public interface IInventoryService
{
    Task<IReadOnlyList<InventoryItemDto>> ListAsync(Guid householdId, InventoryItemStatus status, CancellationToken ct);

    Task<Result<InventoryItemDto>> GetAsync(Guid householdId, Guid itemId, CancellationToken ct);

    Task<Result<InventoryItemDto>> CreateAsync(Guid actorId, Guid householdId, SaveInventoryItemRequest request, CancellationToken ct);

    /// <summary>
    /// Ajout groupé (lignes validées d'un ticket de caisse) : tout ou rien, dans une seule
    /// transaction. Une erreur de validation désigne la ligne fautive (« Items[3].CategoryId »).
    /// </summary>
    Task<Result<IReadOnlyList<InventoryItemDto>>> CreateManyAsync(
        Guid actorId, Guid householdId, IReadOnlyList<SaveInventoryItemRequest> requests, CancellationToken ct);

    Task<Result<InventoryItemDto>> UpdateAsync(
        Guid actorId, Guid householdId, Guid itemId, SaveInventoryItemRequest request, CancellationToken ct);

    /// <summary>
    /// Marque le produit « consommé » ou « jeté ». quantity = null (ou toute la quantité) :
    /// le produit entier. Sinon, seule cette part change de statut : elle devient une ligne
    /// d'historique à part, et le produit reste au frigo avec ce qu'il en reste.
    /// Renvoie le produit après le changement (la part restante, s'il en reste).
    /// </summary>
    Task<Result<InventoryItemDto>> ChangeStatusAsync(
        Guid actorId, Guid householdId, Guid itemId, InventoryItemStatus newStatus, decimal? quantity, CancellationToken ct);

    Task<Result> DeleteAsync(Guid actorId, Guid householdId, Guid itemId, CancellationToken ct);
}

public sealed class InventoryService(AppDbContext db, TimeProvider time) : IInventoryService
{
    // Garde-fou contre les abus (et les requêtes qui deviendraient lentes).
    public const int MaxActiveItemsPerHousehold = 500;

    public async Task<IReadOnlyList<InventoryItemDto>> ListAsync(
        Guid householdId, InventoryItemStatus status, CancellationToken ct) =>
        await ItemsOf(householdId)
            .Where(i => i.Status == status)
            // Priorité anti-gaspi : ce qui périme en premier arrive en tête.
            .OrderBy(i => i.ExpiresOn)
            .ThenBy(i => i.Name)
            .Select(ToDtoExpression)
            .ToListAsync(ct);

    public async Task<Result<InventoryItemDto>> GetAsync(Guid householdId, Guid itemId, CancellationToken ct)
    {
        var item = await ItemsOf(householdId)
            .Where(i => i.Id == itemId)
            .Select(ToDtoExpression)
            .SingleOrDefaultAsync(ct);

        return item is null ? InventoryErrors.ItemNotFound : item;
    }

    public async Task<Result<InventoryItemDto>> CreateAsync(
        Guid actorId, Guid householdId, SaveInventoryItemRequest request, CancellationToken ct)
    {
        var created = await AddAsync(actorId, householdId, [request], indexErrors: false, ct);
        return created.IsSuccess ? await GetAsync(householdId, created.Value[0], ct) : created.Error;
    }

    public async Task<Result<IReadOnlyList<InventoryItemDto>>> CreateManyAsync(
        Guid actorId, Guid householdId, IReadOnlyList<SaveInventoryItemRequest> requests, CancellationToken ct)
    {
        var created = await AddAsync(actorId, householdId, requests, indexErrors: true, ct);
        if (!created.IsSuccess)
        {
            return created.Error;
        }

        var ids = created.Value;
        return await ItemsOf(householdId)
            .Where(i => ids.Contains(i.Id))
            .OrderBy(i => i.ExpiresOn)
            .ThenBy(i => i.Name)
            .Select(ToDtoExpression)
            .ToListAsync(ct);
    }

    public async Task<Result<InventoryItemDto>> UpdateAsync(
        Guid actorId, Guid householdId, Guid itemId, SaveInventoryItemRequest request, CancellationToken ct)
    {
        var (item, error) = await FindModifiableAsync(actorId, householdId, itemId, ct);
        if (item is null)
        {
            return error!;
        }

        if (item.Status != InventoryItemStatus.Active)
        {
            return InventoryErrors.NotActive;
        }

        var applied = await ApplyAsync(item, request, actorId, ct);
        if (applied is not null)
        {
            return applied;
        }

        await db.SaveChangesAsync(ct);
        return await GetAsync(householdId, item.Id, ct);
    }

    public async Task<Result<InventoryItemDto>> ChangeStatusAsync(
        Guid actorId, Guid householdId, Guid itemId, InventoryItemStatus newStatus, decimal? quantity, CancellationToken ct)
    {
        if (newStatus == InventoryItemStatus.Active)
        {
            throw new ArgumentException("Seuls « consommé » et « jeté » sont des changements de statut.", nameof(newStatus));
        }

        var changed = await db.InTransactionAsync(async () =>
        {
            // Verrou : deux membres qui mangent « les mêmes 200 g » en même temps sont traités
            // l'un après l'autre (sinon, les deux liraient 600 g et en laisseraient 400).
            if (!await db.LockHouseholdAsync(householdId, ct))
            {
                return (Result<Guid>)InventoryErrors.ItemNotFound;
            }

            var (item, error) = await FindModifiableAsync(actorId, householdId, itemId, ct);
            if (item is null)
            {
                return error!;
            }

            if (item.Status != InventoryItemStatus.Active)
            {
                return InventoryErrors.NotActive;
            }

            if (quantity > item.Quantity)
            {
                return InventoryErrors.QuantityExceedsStock;
            }

            var now = time.GetUtcNow();
            if (quantity is { } part && part < item.Quantity)
            {
                // Consommation partielle : la part mangée ou jetée devient une ligne d'historique
                // (base du futur compteur de gaspillage) ; le produit garde son Id, ses rappels et
                // ses références dans les recettes, avec la quantité restante.
                db.InventoryItems.Add(new InventoryItem
                {
                    HouseholdId = item.HouseholdId,
                    Name = item.Name,
                    CategoryId = item.CategoryId,
                    Quantity = part,
                    Unit = item.Unit,
                    PurchasedOn = item.PurchasedOn,
                    ExpiresOn = item.ExpiresOn,
                    ExpiryIsEstimated = item.ExpiryIsEstimated,
                    Barcode = item.Barcode,
                    OwnerUserId = item.OwnerUserId,
                    Status = newStatus,
                    StatusChangedAt = now,
                    CreatedByUserId = item.CreatedByUserId,
                    CreatedAt = item.CreatedAt,
                    UpdatedAt = now,
                });
                item.Quantity -= part;
            }
            else
            {
                item.Status = newStatus;
                item.StatusChangedAt = now;
            }

            item.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            return item.Id;
        }, ct);

        return changed.IsSuccess ? await GetAsync(householdId, changed.Value, ct) : changed.Error;
    }

    public async Task<Result> DeleteAsync(Guid actorId, Guid householdId, Guid itemId, CancellationToken ct)
    {
        var (item, error) = await FindModifiableAsync(actorId, householdId, itemId, ct);
        if (item is null)
        {
            return error!;
        }

        db.InventoryItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>
    /// Ajoute un ou plusieurs produits : tous sont enregistrés, ou aucun.
    /// </summary>
    private Task<Result<List<Guid>>> AddAsync(
        Guid actorId, Guid householdId, IReadOnlyList<SaveInventoryItemRequest> requests, bool indexErrors, CancellationToken ct) =>
        db.InTransactionAsync(async () =>
        {
            // Verrou : deux ajouts simultanés ne peuvent pas dépasser le plafond ensemble.
            if (!await db.LockHouseholdAsync(householdId, ct))
            {
                return (Result<List<Guid>>)HouseholdErrors.NotFound;
            }

            var activeCount = await db.InventoryItems
                .CountAsync(i => i.HouseholdId == householdId && i.Status == InventoryItemStatus.Active, ct);
            if (activeCount + requests.Count > MaxActiveItemsPerHousehold)
            {
                return InventoryErrors.LimitReached;
            }

            var now = time.GetUtcNow();
            var items = new List<InventoryItem>();
            for (var index = 0; index < requests.Count; index++)
            {
                var request = requests[index];
                var item = new InventoryItem
                {
                    HouseholdId = householdId,
                    Name = request.Name,
                    CreatedByUserId = actorId,
                    CreatedAt = now,
                };

                var applied = await ApplyAsync(item, request, actorId, ct);
                if (applied is not null)
                {
                    // Rien n'a encore été enregistré : la transaction est simplement abandonnée.
                    return indexErrors ? ForLine(applied, index) : applied;
                }
                items.Add(item);
            }

            db.InventoryItems.AddRange(items);
            await db.SaveChangesAsync(ct);
            return items.Select(i => i.Id).ToList();
        }, ct);

    // Erreur de validation d'une ligne d'un ajout groupé : le champ fautif est préfixé par sa ligne.
    private static Error ForLine(Error error, int index) => error.ValidationErrors is null
        ? error
        : error with
        {
            ValidationErrors = error.ValidationErrors.ToDictionary(
                e => $"{nameof(CreateInventoryItemsRequest.Items)}[{index}].{e.Key}", e => e.Value),
        };

    /// <summary>
    /// Un produit commun est modifiable par tout membre ; un produit perso, par son seul propriétaire.
    /// </summary>
    public static bool CanModify(Guid? ownerUserId, Guid actorId) => ownerUserId is null || ownerUserId == actorId;

    // Toutes les requêtes partent d'ici : le filtre sur le foyer ne peut pas être oublié.
    private IQueryable<InventoryItem> ItemsOf(Guid householdId) =>
        db.InventoryItems.AsNoTracking().Where(i => i.HouseholdId == householdId);

    private async Task<(InventoryItem? Item, Error? Error)> FindModifiableAsync(
        Guid actorId, Guid householdId, Guid itemId, CancellationToken ct)
    {
        // Filtre sur l'Id ET le foyer de l'URL : un produit d'un autre foyer est « introuvable ».
        var item = await db.InventoryItems.SingleOrDefaultAsync(i => i.Id == itemId && i.HouseholdId == householdId, ct);

        if (item is null)
        {
            return (null, InventoryErrors.ItemNotFound);
        }

        return CanModify(item.OwnerUserId, actorId) ? (item, null) : (null, InventoryErrors.NotOwner);
    }

    /// <summary>
    /// Valide la demande et l'applique au produit. Renvoie une erreur, ou null si tout est valide.
    /// </summary>
    private async Task<Error?> ApplyAsync(InventoryItem item, SaveInventoryItemRequest request, Guid actorId, CancellationToken ct)
    {
        var category = await db.ProductCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == request.CategoryId, ct);
        if (category is null)
        {
            return InventoryErrors.UnknownCategory;
        }

        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        // Demain toléré : l'utilisateur peut déjà être le lendemain dans son fuseau.
        if (request.PurchasedOn > today.AddDays(1) || request.PurchasedOn < today.AddYears(-5))
        {
            return InventoryErrors.PurchaseDateOutOfRange;
        }

        var expiresOn = request.ExpiresOn ?? ExpiryEstimator.Estimate(request.PurchasedOn, category.DefaultShelfLifeDays);
        // Un produit déjà dépassé à l'achat est possible (rayon anti-gaspi), dans une limite raisonnable.
        if (expiresOn < request.PurchasedOn.AddYears(-1) || expiresOn > request.PurchasedOn.AddYears(10))
        {
            return InventoryErrors.ExpiryDateOutOfRange;
        }

        item.Name = request.Name.Trim();
        item.CategoryId = category.Id;
        item.Quantity = request.Quantity;
        item.Unit = request.Unit;
        item.PurchasedOn = request.PurchasedOn;
        item.ExpiresOn = expiresOn;
        item.ExpiryIsEstimated = request.ExpiresOn is null;
        item.Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode;
        // Seul le propriétaire (ou n'importe qui pour un produit commun) arrive jusqu'ici :
        // passer en perso attribue le produit à l'auteur de la demande.
        item.OwnerUserId = request.IsPersonal ? actorId : null;
        item.UpdatedAt = time.GetUtcNow();
        return null;
    }

    // Projection SQL directe vers le DTO (un seul aller-retour, pas d'entité chargée).
    private static readonly System.Linq.Expressions.Expression<Func<InventoryItem, InventoryItemDto>> ToDtoExpression =
        i => new InventoryItemDto(
            i.Id,
            i.Name,
            i.CategoryId,
            i.Quantity,
            i.Unit,
            i.PurchasedOn,
            i.ExpiresOn,
            i.ExpiryIsEstimated,
            i.Category.ExpiryKind,
            i.Barcode,
            i.OwnerUserId,
            i.Owner == null ? null : i.Owner.DisplayName,
            i.Status,
            i.StatusChangedAt,
            i.CreatedAt);
}

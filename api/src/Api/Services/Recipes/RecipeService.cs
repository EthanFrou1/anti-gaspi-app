using System.Text.Json;
using Api.Common;
using Api.Data;
using Api.Dtos.Recipes;
using Api.Entities;
using Api.Options;
using Api.Services.Ai;
using Api.Services.Inventory;
using Api.Services.Profiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services.Recipes;

public static class RecipeErrors
{
    public static readonly Error DailyQuotaReached = new(
        ErrorType.TooManyRequests, "recipe.daily_quota", "Tu as utilisé toutes tes recettes du jour. Reviens demain !");

    public static readonly Error GlobalQuotaReached = new(
        ErrorType.TooManyRequests, "recipe.global_quota",
        "Beaucoup de recettes ont déjà été générées aujourd'hui sur l'app. Réessaie demain !");

    // Message volontairement générique : la cause précise (refus, panne…) reste dans les journaux.
    public static readonly Error Unavailable = new(
        ErrorType.Unavailable, "recipe.unavailable",
        "La génération de recette est momentanément indisponible. Réessaie dans un instant.");

    public static readonly Error NotFound = new(ErrorType.NotFound, "recipe.not_found", "Recette introuvable.");

    public static readonly Error InvalidDiners = new(
        ErrorType.Validation, "recipe.invalid_diners", "Les convives doivent être des membres du foyer.",
        new Dictionary<string, string[]>
        {
            [nameof(GenerateRecipeRequest.DinerUserIds)] = ["Les convives doivent être des membres du foyer."],
        });

    public static readonly Error FavoriteLimitReached = new(
        ErrorType.Conflict, "recipe.favorite_limit",
        $"Tu as atteint la limite de {RecipeService.MaxFavoritesPerUser} recettes favorites. Retire une étoile pour en ajouter une.");

    public static readonly Error InvalidCookedItems = new(
        ErrorType.Validation, "recipe.invalid_items", "Ces produits ne font pas partie de la recette.",
        new Dictionary<string, string[]>
        {
            [nameof(MarkRecipeCookedRequest.FinishedItemIds)] = ["Ces produits ne font pas partie de la recette."],
        });
}

public interface IRecipeService
{
    Task<Result<RecipePromptPreviewDto>> PreviewPromptAsync(Guid actorId, Guid householdId, GenerateRecipeRequest request, CancellationToken ct);

    Task<Result<RecipeDto>> GenerateAsync(Guid actorId, Guid householdId, GenerateRecipeRequest request, CancellationToken ct);

    Task<RecipeQuotaDto> GetQuotaAsync(Guid userId, CancellationToken ct);

    Task<IReadOnlyList<RecipeDto>> ListAsync(Guid viewerId, Guid householdId, CancellationToken ct);

    /// <summary>Carnet commun : recettes du foyer étoilées par au moins un membre.</summary>
    Task<IReadOnlyList<RecipeDto>> ListFavoritesAsync(Guid viewerId, Guid householdId, CancellationToken ct);

    Task<Result<RecipeDto>> GetAsync(Guid viewerId, Guid householdId, Guid recipeId, CancellationToken ct);

    Task<Result<RecipeDto>> AddFavoriteAsync(Guid userId, Guid householdId, Guid recipeId, CancellationToken ct);

    Task<Result<RecipeDto>> RemoveFavoriteAsync(Guid userId, Guid householdId, Guid recipeId, CancellationToken ct);

    Task<Result<int>> MarkCookedAsync(Guid actorId, Guid householdId, Guid recipeId, IReadOnlyList<Guid> finishedItemIds, CancellationToken ct);

    /// <summary>
    /// Nettoyage périodique : historique de plus de 30 jours, réservations abandonnées.
    /// </summary>
    Task<int> PurgeAsync(CancellationToken ct);
}

public sealed class RecipeService(
    AppDbContext db,
    IRecipeGenerator generator,
    IOptions<AiOptions> options,
    TimeProvider time,
    ILogger<RecipeService> logger) : IRecipeService
{
    public static readonly TimeSpan HistoryRetention = TimeSpan.FromDays(30);

    public const int MaxFavoritesPerUser = 200;

    // Une réservation encore vide après ce délai correspond à un crash pendant l'appel à l'IA.
    public static readonly TimeSpan StaleReservation = TimeSpan.FromMinutes(10);

    // Clé du verrou PostgreSQL qui sérialise la vérification des quotas (valeur arbitraire, fixe).
    private const long QuotaLockKey = 4_242_001;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private AiOptions Settings => options.Value;

    public async Task<Result<RecipePromptPreviewDto>> PreviewPromptAsync(
        Guid actorId, Guid householdId, GenerateRecipeRequest request, CancellationToken ct)
    {
        var prompt = await BuildPromptAsync(actorId, householdId, request, ct);
        if (!prompt.IsSuccess)
        {
            return prompt.Error;
        }

        var p = prompt.Value;
        var schema = JsonSerializer.SerializeToElement(p.OutputSchema);
        var clipboard =
            $"""
            === PROMPT SYSTÈME ===
            {p.System}

            === MESSAGE UTILISATEUR ===
            {p.UserContent}

            === FORMAT DE RÉPONSE IMPOSÉ (JSON Schema) ===
            {RecipeOutputSchema.Json}
            """;
        return new RecipePromptPreviewDto(Settings.Model, Settings.MaxOutputTokens, p.System, p.UserContent, schema, clipboard);
    }

    public async Task<Result<RecipeDto>> GenerateAsync(
        Guid actorId, Guid householdId, GenerateRecipeRequest request, CancellationToken ct)
    {
        var prompt = await BuildPromptAsync(actorId, householdId, request, ct);
        if (!prompt.IsSuccess)
        {
            return prompt.Error;
        }

        // 1. Réservation d'une place dans les quotas (courte transaction, sous verrou).
        var reservation = await ReserveAsync(actorId, householdId, ct);
        if (!reservation.IsSuccess)
        {
            return reservation.Error;
        }

        // 2. Appel à l'IA HORS transaction : quelques secondes sans bloquer les autres utilisateurs.
        ValidatedRecipe recipe;
        try
        {
            var draft = await generator.GenerateAsync(prompt.Value, ct);
            recipe = RecipeValidator.Validate(draft, prompt.Value);
        }
        catch (AiUnavailableException ex)
        {
            logger.LogWarning("Recette non générée : {Reason}", ex.Message);
            await CancelReservationAsync(reservation.Value);
            return RecipeErrors.Unavailable;
        }
        catch
        {
            // Toute autre erreur (y compris l'annulation) : la place réservée est libérée.
            await CancelReservationAsync(reservation.Value);
            throw;
        }

        // 3. La réservation devient une entrée d'historique.
        var json = JsonSerializer.Serialize(recipe, JsonOptions);
        await db.RecipeGenerations
            .Where(r => r.Id == reservation.Value)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.RecipeJson, json), ct);

        return (await GetAsync(actorId, householdId, reservation.Value, ct)).Value!;
    }

    public async Task<RecipeQuotaDto> GetQuotaAsync(Guid userId, CancellationToken ct)
    {
        var (start, end, _) = RecipeDay.Window(time.GetUtcNow(), Zone());
        var used = await db.RecipeGenerations.CountAsync(r => r.RequestedByUserId == userId && r.CreatedAt >= start, ct);
        var limit = Settings.DailyLimitPerUser;
        return new RecipeQuotaDto(used, limit, Math.Max(0, limit - used), end);
    }

    public async Task<IReadOnlyList<RecipeDto>> ListAsync(Guid viewerId, Guid householdId, CancellationToken ct)
    {
        var since = time.GetUtcNow() - HistoryRetention;
        var rows = await CompletedRecipes(householdId)
            .Where(r => r.CreatedAt >= since)
            .OrderByDescending(r => r.CreatedAt)
            .Take(100)
            .Select(Project(viewerId))
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<RecipeDto>> ListFavoritesAsync(Guid viewerId, Guid householdId, CancellationToken ct)
    {
        var rows = await CompletedRecipes(householdId)
            .Where(r => r.Favorites.Any())
            // La dernière étoile posée en premier.
            .OrderByDescending(r => r.Favorites.Max(f => f.CreatedAt))
            .Select(Project(viewerId))
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<Result<RecipeDto>> GetAsync(Guid viewerId, Guid householdId, Guid recipeId, CancellationToken ct)
    {
        var row = await CompletedRecipes(householdId)
            .Where(r => r.Id == recipeId)
            .Select(Project(viewerId))
            .SingleOrDefaultAsync(ct);
        return row is null ? RecipeErrors.NotFound : ToDto(row);
    }

    public async Task<Result<RecipeDto>> AddFavoriteAsync(Guid userId, Guid householdId, Guid recipeId, CancellationToken ct)
    {
        var result = await db.InTransactionAsync(async () =>
        {
            // Verrou propre à l'utilisateur : deux ajouts simultanés ne dépassent pas le plafond.
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({userId.ToString()}))", ct);

            if (!await CompletedRecipes(householdId).AnyAsync(r => r.Id == recipeId, ct))
            {
                return (Result)RecipeErrors.NotFound;
            }

            if (await db.RecipeFavorites.AnyAsync(f => f.RecipeGenerationId == recipeId && f.UserId == userId, ct))
            {
                return Result.Success(); // déjà en favori : rien à faire
            }

            if (await db.RecipeFavorites.CountAsync(f => f.UserId == userId, ct) >= MaxFavoritesPerUser)
            {
                return RecipeErrors.FavoriteLimitReached;
            }

            db.RecipeFavorites.Add(new RecipeFavorite { RecipeGenerationId = recipeId, UserId = userId, CreatedAt = time.GetUtcNow() });
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);

        return result.IsSuccess ? await GetAsync(userId, householdId, recipeId, ct) : result.Error;
    }

    public async Task<Result<RecipeDto>> RemoveFavoriteAsync(Guid userId, Guid householdId, Guid recipeId, CancellationToken ct)
    {
        if (!await CompletedRecipes(householdId).AnyAsync(r => r.Id == recipeId, ct))
        {
            return RecipeErrors.NotFound;
        }

        // Retire uniquement SON étoile : celles des autres membres restent.
        await db.RecipeFavorites
            .Where(f => f.RecipeGenerationId == recipeId && f.UserId == userId)
            .ExecuteDeleteAsync(ct);
        return await GetAsync(userId, householdId, recipeId, ct);
    }

    public async Task<Result<int>> MarkCookedAsync(
        Guid actorId, Guid householdId, Guid recipeId, IReadOnlyList<Guid> finishedItemIds, CancellationToken ct)
    {
        var recipe = await GetAsync(actorId, householdId, recipeId, ct);
        if (!recipe.IsSuccess)
        {
            return recipe.Error;
        }

        // Seuls les produits du frigo utilisés par CETTE recette peuvent être cochés.
        var recipeItemIds = recipe.Value.Ingredients.Select(i => i.InventoryItemId).OfType<Guid>().ToHashSet();
        var requested = finishedItemIds.Distinct().ToList();
        if (requested.Any(id => !recipeItemIds.Contains(id)))
        {
            return RecipeErrors.InvalidCookedItems;
        }

        return await db.InTransactionAsync(async () =>
        {
            var items = await db.InventoryItems
                .Where(i => i.HouseholdId == householdId && requested.Contains(i.Id))
                .ToListAsync(ct);

            // Mêmes droits que « consommé » à l'unité : pas les produits perso des autres.
            if (items.Any(i => !InventoryService.CanModify(i.OwnerUserId, actorId)))
            {
                return (Result<int>)InventoryErrors.NotOwner;
            }

            var now = time.GetUtcNow();
            // Un produit déjà consommé, jeté ou supprimé entre-temps est simplement ignoré.
            var active = items.Where(i => i.Status == InventoryItemStatus.Active).ToList();
            foreach (var item in active)
            {
                item.Status = InventoryItemStatus.Consumed;
                item.StatusChangedAt = now;
                item.UpdatedAt = now;
            }
            await db.SaveChangesAsync(ct);
            return active.Count;
        }, ct);
    }

    public async Task<int> PurgeAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var historyLimit = now - HistoryRetention;
        var staleLimit = now - StaleReservation;
        return await db.RecipeGenerations
            // Une recette en favori est conservée sans limite de durée.
            .Where(r => (r.CreatedAt < historyLimit && !r.Favorites.Any())
                        || (r.RecipeJson == null && r.CreatedAt < staleLimit))
            .ExecuteDeleteAsync(ct);
    }

    // ---------- Construction du prompt (partagée par la génération et l'aperçu) ----------

    private async Task<Result<RecipePrompt>> BuildPromptAsync(
        Guid actorId, Guid householdId, GenerateRecipeRequest request, CancellationToken ct)
    {
        var diners = (request.DinerUserIds is { Count: > 0 } ids ? ids : [actorId]).Distinct().ToList();

        // Tous les convives doivent appartenir à CE foyer.
        var memberCount = await db.HouseholdMembers
            .CountAsync(m => m.HouseholdId == householdId && diners.Contains(m.UserId), ct);
        if (memberCount != diners.Count)
        {
            return RecipeErrors.InvalidDiners;
        }

        var profiles = await db.UserProfiles.AsNoTracking()
            .Where(p => diners.Contains(p.UserId))
            .ToListAsync(ct);
        // Membre sans profil (compte créé avant l'onboarding) : valeurs par défaut.
        var dinerProfiles = diners
            .Select(id => profiles.SingleOrDefault(p => p.UserId == id) ?? new UserProfile { UserId = id })
            .ToList();

        var equipment = await db.Households.AsNoTracking()
            .Where(h => h.Id == householdId)
            .Select(h => h.Equipment)
            .SingleAsync(ct);

        var constraints = ProfileCombiner.Combine(dinerProfiles, equipment, request.Servings);

        var inventory = await db.InventoryItems.AsNoTracking()
            .Where(i => i.HouseholdId == householdId && i.Status == InventoryItemStatus.Active)
            .Select(i => new PromptItem(i.Id, i.Name, i.Category.Code, i.Quantity, i.Unit, i.ExpiresOn, i.Category.ExpiryKind))
            .ToListAsync(ct);

        var today = RecipeDay.Window(time.GetUtcNow(), Zone()).LocalDate;
        return RecipePromptBuilder.Build(constraints, inventory, today);
    }

    // ---------- Quotas ----------

    private Task<Result<Guid>> ReserveAsync(Guid userId, Guid householdId, CancellationToken ct) =>
        db.InTransactionAsync(async () =>
        {
            // Verrou PostgreSQL tenu jusqu'à la fin de la transaction : deux demandes simultanées
            // sont traitées l'une après l'autre, et ne peuvent pas dépasser un quota ensemble.
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({QuotaLockKey})", ct);

            var now = time.GetUtcNow();
            var (start, _, _) = RecipeDay.Window(now, Zone());

            // Les réservations en cours comptent : une génération lancée occupe sa place.
            var usedByUser = await db.RecipeGenerations.CountAsync(r => r.RequestedByUserId == userId && r.CreatedAt >= start, ct);
            if (usedByUser >= Settings.DailyLimitPerUser)
            {
                return (Result<Guid>)RecipeErrors.DailyQuotaReached;
            }

            var usedGlobally = await db.RecipeGenerations.CountAsync(r => r.CreatedAt >= start, ct);
            if (usedGlobally >= Settings.GlobalDailyLimit)
            {
                logger.LogWarning("Limite globale quotidienne de recettes atteinte ({Limit}).", Settings.GlobalDailyLimit);
                return RecipeErrors.GlobalQuotaReached;
            }

            var reservation = new RecipeGeneration { HouseholdId = householdId, RequestedByUserId = userId, CreatedAt = now };
            db.RecipeGenerations.Add(reservation);
            await db.SaveChangesAsync(ct);
            return reservation.Id;
        }, ct);

    private async Task CancelReservationAsync(Guid reservationId)
    {
        // CancellationToken.None : la place doit être libérée même si la requête a été annulée.
        await db.RecipeGenerations
            .Where(r => r.Id == reservationId && r.RecipeJson == null)
            .ExecuteDeleteAsync(CancellationToken.None);
    }

    private TimeZoneInfo Zone() => TimeZoneInfo.FindSystemTimeZoneById(Settings.TimeZone);

    // Recettes terminées du foyer de l'URL : la recette d'un autre foyer est « introuvable » (IDOR).
    private IQueryable<RecipeGeneration> CompletedRecipes(Guid householdId) =>
        db.RecipeGenerations.AsNoTracking().Where(r => r.HouseholdId == householdId && r.RecipeJson != null);

    private sealed record RecipeRow(RecipeGeneration Row, int FavoriteCount, bool IsFavorite);

    // Projection finale (après filtres et tris) : la recette, son nombre d'étoiles et la mienne.
    private static System.Linq.Expressions.Expression<Func<RecipeGeneration, RecipeRow>> Project(Guid viewerId) =>
        r => new RecipeRow(r, r.Favorites.Count, r.Favorites.Any(f => f.UserId == viewerId));

    private static RecipeDto ToDto(RecipeRow data)
    {
        var row = data.Row;
        var recipe = JsonSerializer.Deserialize<ValidatedRecipe>(row.RecipeJson!, JsonOptions)!;
        return new RecipeDto(
            row.Id,
            recipe.Title,
            recipe.PrepMinutes,
            recipe.Servings,
            recipe.Ingredients.Select(i => new RecipeIngredientDto(i.Name, i.Quantity, i.InventoryItemId)).ToList(),
            recipe.Steps,
            row.CreatedAt,
            data.IsFavorite,
            data.FavoriteCount);
    }
}

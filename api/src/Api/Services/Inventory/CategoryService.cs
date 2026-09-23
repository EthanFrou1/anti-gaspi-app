using Api.Data;
using Api.Dtos.Inventory;
using Microsoft.EntityFrameworkCore;

namespace Api.Services.Inventory;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken ct);

    /// <summary>
    /// Règles de correspondance Open Food Facts, indexées par étiquette.
    /// </summary>
    Task<IReadOnlyDictionary<string, OffTagRule>> GetOffRulesAsync(CancellationToken ct);
}

public sealed class CategoryService(AppDbContext db) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken ct) =>
        await db.ProductCategories
            .AsNoTracking()
            // Tri par Id : l'ordre des données de référence regroupe les familles
            // (viandes, produits laitiers, fruits et légumes…), plus pratique qu'un tri alphabétique.
            .OrderBy(c => c.Id)
            .Select(c => new CategoryDto(c.Id, c.Code, c.Name, c.DefaultShelfLifeDays, c.ExpiryKind))
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<string, OffTagRule>> GetOffRulesAsync(CancellationToken ct) =>
        await db.OffCategoryMappings
            .AsNoTracking()
            .ToDictionaryAsync(m => m.OffTag, m => new OffTagRule(m.CategoryId, m.Priority), ct);
}

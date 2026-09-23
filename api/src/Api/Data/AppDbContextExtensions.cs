using Api.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Data;

public static class AppDbContextExtensions
{
    /// <summary>
    /// Exécute l'action dans une transaction, validée seulement si le résultat est un succès.
    /// Si une transaction est déjà ouverte (appel imbriqué), l'action y participe
    /// et c'est l'appelant qui décide de valider.
    /// </summary>
    public static async Task<TResult> InTransactionAsync<TResult>(
        this AppDbContext db, Func<Task<TResult>> action, CancellationToken ct)
        where TResult : IOperationResult
    {
        if (db.Database.CurrentTransaction is not null)
        {
            return await action();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var result = await action();
        if (result.IsSuccess)
        {
            await transaction.CommitAsync(ct);
        }

        // Sinon : la transaction est annulée automatiquement à sa libération (Dispose).
        return result;
    }

    /// <summary>
    /// Verrouille la ligne du foyer jusqu'à la fin de la transaction en cours
    /// (SELECT … FOR UPDATE). Les autres opérations sur le même foyer attendent :
    /// les changements de membres sont ainsi traités l'un après l'autre.
    /// Renvoie false si le foyer n'existe pas (ou plus).
    /// </summary>
    public static async Task<bool> LockHouseholdAsync(this AppDbContext db, Guid householdId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Le verrouillage d'un foyer doit se faire dans une transaction.");
        }

        var ids = await db.Database
            .SqlQuery<Guid>($"""SELECT "Id" AS "Value" FROM "Households" WHERE "Id" = {householdId} FOR UPDATE""")
            .ToListAsync(ct);

        return ids.Count == 1;
    }

    public static bool IsUniqueViolation(this DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

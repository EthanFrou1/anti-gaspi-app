using System.Collections.Concurrent;

namespace Api.Services.Ai;

/// <summary>
/// Tokens consommés depuis le démarrage de l'API, par opération et par modèle. En mémoire
/// seulement, remis à zéro au redémarrage, sans aucune donnée personnelle (des compteurs).
/// Lu par l'outil de développement GET /api/dev/ai-usage (script d'évaluation des tickets).
/// </summary>
public sealed class AiUsageMeter
{
    private readonly ConcurrentDictionary<(string Operation, string Model), Counters> _counters = new();

    public void Record(string operation, string model, long inputTokens, long cacheReadTokens, long cacheWriteTokens, long outputTokens)
    {
        var counters = _counters.GetOrAdd((operation, model), _ => new Counters());
        Interlocked.Increment(ref counters.Calls);
        Interlocked.Add(ref counters.InputTokens, inputTokens);
        Interlocked.Add(ref counters.CacheReadTokens, cacheReadTokens);
        Interlocked.Add(ref counters.CacheWriteTokens, cacheWriteTokens);
        Interlocked.Add(ref counters.OutputTokens, outputTokens);
    }

    public IReadOnlyList<AiUsage> Snapshot() =>
        _counters
            .Select(entry => new AiUsage(
                entry.Key.Operation,
                entry.Key.Model,
                Interlocked.Read(ref entry.Value.Calls),
                Interlocked.Read(ref entry.Value.InputTokens),
                Interlocked.Read(ref entry.Value.CacheReadTokens),
                Interlocked.Read(ref entry.Value.CacheWriteTokens),
                Interlocked.Read(ref entry.Value.OutputTokens)))
            .OrderBy(u => u.Operation, StringComparer.Ordinal)
            .ThenBy(u => u.Model, StringComparer.Ordinal)
            .ToList();

    // Champs (et non propriétés) : Interlocked a besoin d'une référence vers chaque compteur.
    private sealed class Counters
    {
        public long Calls;
        public long InputTokens;
        public long CacheReadTokens;
        public long CacheWriteTokens;
        public long OutputTokens;
    }
}

public sealed record AiUsage(
    string Operation,
    string Model,
    long Calls,
    long InputTokens,
    long CacheReadTokens,
    long CacheWriteTokens,
    long OutputTokens);

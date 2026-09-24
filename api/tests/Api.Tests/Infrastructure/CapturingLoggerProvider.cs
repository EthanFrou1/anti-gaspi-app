using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Journal de test : garde chaque message, avec son exception complète, pour vérifier ce que
/// l'API écrit (ou n'écrit pas) dans ses journaux.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _entries = new();

    public IReadOnlyCollection<string> Entries => _entries;

    public string AllText => string.Join('\n', _entries);

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<string> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue($"[{logLevel}] {category} : {formatter(state, exception)} {exception}");
    }
}

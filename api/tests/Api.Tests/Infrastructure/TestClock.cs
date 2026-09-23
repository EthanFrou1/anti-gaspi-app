namespace Api.Tests.Infrastructure;

/// <summary>
/// Horloge contrôlable pour les tests : permet d'avancer le temps
/// (ex. faire expirer un jeton) sans attendre réellement.
/// </summary>
public sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; private set; } = start;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan duration) => Now += duration;
}

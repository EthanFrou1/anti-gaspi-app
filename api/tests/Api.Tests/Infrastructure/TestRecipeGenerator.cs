using Api.Services.Recipes;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Générateur de test : se comporte comme le Fake, mais peut simuler une panne, une réponse
/// invalide ou une lenteur, et compte ses appels.
/// </summary>
public sealed class TestRecipeGenerator : IRecipeGenerator
{
    private readonly FakeRecipeGenerator _fake = new();
    private int _calls;

    public int Calls => _calls;

    public bool FailWithUnavailable { get; set; }

    // Permet de renvoyer une réponse précise (ex. invalide) à la place de celle du Fake.
    public Func<RecipePrompt, RecipeDraft>? Override { get; set; }

    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    // Dernier prompt reçu : permet de vérifier ce qui serait parti vers l'IA.
    public RecipePrompt? LastPrompt { get; private set; }

    public async Task<RecipeDraft> GenerateAsync(RecipePrompt prompt, CancellationToken ct)
    {
        Interlocked.Increment(ref _calls);
        LastPrompt = prompt;
        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, ct);
        }
        if (FailWithUnavailable)
        {
            throw new RecipeGenerationUnavailableException("panne simulée");
        }
        return Override?.Invoke(prompt) ?? await _fake.GenerateAsync(prompt, ct);
    }
}

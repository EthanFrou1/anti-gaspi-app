using Api.Services.Ai;
using Api.Services.Receipts;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Lecteur de test : se comporte comme le Fake, mais peut simuler une panne ou renvoyer une
/// réponse précise, et compte ses appels.
/// </summary>
public sealed class TestReceiptReader : IReceiptReader
{
    private readonly FakeReceiptReader _fake = new();
    private int _calls;

    public int Calls => _calls;

    public bool FailWithUnavailable { get; set; }

    public Func<ReceiptDraft>? Override { get; set; }

    // Dernière image reçue : permet de vérifier ce qui serait parti vers l'IA.
    public ReceiptImage? LastImage { get; private set; }

    public async Task<ReceiptDraft> ReadAsync(ReceiptImage image, ReceiptPrompt prompt, CancellationToken ct)
    {
        Interlocked.Increment(ref _calls);
        LastImage = image;
        if (FailWithUnavailable)
        {
            throw new AiUnavailableException("panne simulée");
        }
        return Override?.Invoke() ?? await _fake.ReadAsync(image, prompt, ct);
    }
}

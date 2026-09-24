namespace Api.Services.Receipts;

/// <summary>
/// Vérifie qu'un fichier envoyé est bien une image JPEG ou PNG, d'après ses premiers octets
/// (« nombre magique ») et non d'après le type annoncé par le client, qui peut mentir.
/// Fonction pure.
/// </summary>
public static class ReceiptImageFormat
{
    // L'app envoie une photo redimensionnée à 1568 px et compressée : quelques centaines de Ko.
    public const int MaxBytes = 2 * 1024 * 1024;

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Type MIME de l'image (« image/jpeg », « image/png »), ou null si le format est refusé.</summary>
    public static string? Detect(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith(JpegSignature))
        {
            return "image/jpeg";
        }
        return data.StartsWith(PngSignature) ? "image/png" : null;
    }
}

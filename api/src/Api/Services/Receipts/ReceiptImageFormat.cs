namespace Api.Services.Receipts;

/// <summary>
/// Vérifie qu'un fichier envoyé est bien une image JPEG, d'après ses premiers octets
/// (« nombre magique ») et non d'après le type annoncé par le client, qui peut mentir.
/// JPEG uniquement : c'est ce que l'app envoie toujours, et c'est le seul format dont
/// l'API sait retirer les métadonnées (JpegMetadataStripper). Fonction pure.
/// </summary>
public static class ReceiptImageFormat
{
    // L'app envoie une photo redimensionnée à 1568 px et compressée : quelques centaines de Ko.
    public const int MaxBytes = 2 * 1024 * 1024;

    public const string JpegMediaType = "image/jpeg";

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    public static bool IsJpeg(ReadOnlySpan<byte> data) => data.StartsWith(JpegSignature);
}

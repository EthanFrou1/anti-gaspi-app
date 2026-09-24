namespace Api.Tests.Infrastructure;

/// <summary>
/// Images JPEG pour les tests du ticket de caisse.
/// </summary>
public static class TestJpeg
{
    /// <summary>
    /// Vraie photo (générée avec Pillow) : EXIF avec marque, modèle et position GPS, commentaire,
    /// et des données ajoutées après la fin de l'image.
    /// </summary>
    public static byte[] PhotoWithMetadata { get; } =
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Receipts", "Fixtures", "photo-with-gps.jpg"));

    /// <summary>
    /// JPEG de la taille voulue, à la structure valide (segments, données compressées, fin
    /// d'image), mais aux pixels factices : pour les tests de taille et de disque.
    /// </summary>
    public static byte[] OfSize(int totalBytes)
    {
        byte[] header =
        [
            0xFF, 0xD8,                                                  // début de l'image
            0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00,        // APP0 « JFIF »
            0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
            0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00,  // début des données (SOS)
        ];
        var image = new byte[totalBytes];
        header.CopyTo(image, 0);
        // Données compressées factices (des zéros), puis la fin de l'image.
        image[^2] = 0xFF;
        image[^1] = 0xD9;
        return image;
    }

    public static bool Contains(byte[] data, string text) =>
        data.AsSpan().IndexOf(System.Text.Encoding.ASCII.GetBytes(text)) >= 0;
}

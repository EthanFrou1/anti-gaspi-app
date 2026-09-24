namespace Api.Services.Receipts;

/// <summary>
/// Retire d'un JPEG ses métadonnées avant l'envoi à l'IA : EXIF (dont la position GPS et le
/// modèle du téléphone), XMP, IPTC, commentaires, et toute donnée placée après la fin de l'image
/// (certains téléphones y ajoutent des images secondaires avec leur propre EXIF).
/// Les pixels ne sont pas touchés : seuls des segments entiers sont écartés. Fonction pure.
///
/// L'app réencode déjà la photo avant l'envoi (ce qui supprime ces métadonnées) : ceci est une
/// seconde barrière, côté serveur, qui ne dépend pas du client.
///
/// Rappel du format : une suite de segments « FF xx » (xx = type), la plupart suivis de leur
/// longueur sur 2 octets. Après le segment SOS viennent les données compressées de l'image,
/// dans lesquelles un octet FF est toujours suivi de 00 (« bourrage ») ou d'un marqueur.
/// </summary>
public static class JpegMetadataStripper
{
    private const byte Marker = 0xFF;
    private const byte StartOfImage = 0xD8;
    private const byte EndOfImage = 0xD9;
    private const byte StartOfScan = 0xDA;

    /// <summary>Copie sans métadonnées, ou null si la structure du fichier n'est pas celle d'un JPEG.</summary>
    public static byte[]? Strip(ReadOnlySpan<byte> jpeg)
    {
        if (jpeg.Length < 4 || jpeg[0] != Marker || jpeg[1] != StartOfImage)
        {
            return null;
        }

        using var output = new MemoryStream(jpeg.Length);
        output.Write(jpeg[..2]);
        var position = 2;
        var inScan = false;

        while (position < jpeg.Length)
        {
            if (inScan)
            {
                // Données compressées : copiées jusqu'au prochain vrai marqueur.
                var start = position;
                while (position + 1 < jpeg.Length
                       && !(jpeg[position] == Marker && jpeg[position + 1] != 0x00 && !IsRestart(jpeg[position + 1])))
                {
                    position++;
                }
                if (position + 1 >= jpeg.Length)
                {
                    return null; // fin de l'image absente
                }
                output.Write(jpeg[start..position]);
                inScan = false;
                continue;
            }

            if (jpeg[position] != Marker)
            {
                return null;
            }
            // Octets de remplissage (FF FF…) autorisés avant un marqueur.
            while (position < jpeg.Length && jpeg[position] == Marker)
            {
                position++;
            }
            if (position >= jpeg.Length)
            {
                return null;
            }

            var type = jpeg[position++];
            if (type == EndOfImage)
            {
                // Tout ce qui suit la fin de l'image est ignoré.
                output.Write([Marker, EndOfImage]);
                return output.ToArray();
            }
            if (IsRestart(type) || type == 0x01)
            {
                output.Write([Marker, type]); // marqueurs sans longueur
                continue;
            }

            if (position + 2 > jpeg.Length)
            {
                return null;
            }
            var length = (jpeg[position] << 8) | jpeg[position + 1];
            if (length < 2 || position + length > jpeg.Length)
            {
                return null;
            }

            if (!IsMetadata(type))
            {
                output.Write([Marker, type]);
                output.Write(jpeg.Slice(position, length));
            }
            position += length;
            inScan = type == StartOfScan;
        }

        return null; // fin de l'image absente
    }

    // APP1 à APP13 et APP15 (EXIF, XMP, IPTC, MPF…) et COM (commentaire). APP0 (JFIF) et
    // APP14 (Adobe, qui décrit l'espace de couleurs) sont conservés : ils ne contiennent rien de personnel.
    private static bool IsMetadata(byte type) => type is (>= 0xE1 and <= 0xED) or 0xEF or 0xFE;

    private static bool IsRestart(byte type) => type is >= 0xD0 and <= 0xD7;
}

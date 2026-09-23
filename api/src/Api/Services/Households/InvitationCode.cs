using System.Security.Cryptography;

namespace Api.Services.Households;

/// <summary>
/// Codes d'invitation courts, faciles à dicter ou recopier.
/// </summary>
public static class InvitationCode
{
    public const int Length = 8;

    // 32 caractères, sans ceux qu'on confond (0/O, 1/I) :
    // 32^8 ≈ 1 100 milliards de combinaisons, impossible à deviner avec le rate limiting.
    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Generate() =>
        new(RandomNumberGenerator.GetItems<char>(Alphabet, Length));

    /// <summary>
    /// Tolère la saisie de l'utilisateur : minuscules, espaces, tirets (« abcd-efgh »).
    /// </summary>
    public static string Normalize(string input) =>
        new string(input.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}

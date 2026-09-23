namespace Api.Common;

public enum ErrorType
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
}

/// <summary>
/// Erreur métier attendue (mauvais mot de passe, email déjà pris…).
/// Le code est stable et exploitable par l'app mobile ; le message est affichable.
/// </summary>
public sealed record Error(
    ErrorType Type,
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);

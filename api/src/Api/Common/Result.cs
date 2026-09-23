using System.Diagnostics.CodeAnalysis;

namespace Api.Common;

/// <summary>
/// Point commun des résultats, pour du code générique (ex. valider une transaction
/// seulement si l'opération a réussi).
/// </summary>
public interface IOperationResult
{
    Error? Error { get; }

    bool IsSuccess { get; }
}

/// <summary>
/// Résultat d'une opération métier : soit une valeur, soit une erreur.
/// Évite d'utiliser des exceptions pour des cas prévus (identifiants invalides, etc.).
/// </summary>
public sealed class Result<T> : IOperationResult
{
    private Result(T value) => Value = value;

    private Result(Error error) => Error = error;

    public T? Value { get; }

    public Error? Error { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    // Conversions implicites : un service peut écrire « return user; » ou « return AuthErrors.X; ».
    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error) => new(error);
}

/// <summary>
/// Résultat d'une opération qui ne renvoie pas de valeur.
/// </summary>
public sealed class Result : IOperationResult
{
    private static readonly Result SuccessInstance = new(null);

    private Result(Error? error) => Error = error;

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result Success() => SuccessInstance;

    public static implicit operator Result(Error error) => new(error);
}

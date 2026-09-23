using System.Diagnostics.CodeAnalysis;

namespace Api.Common;

/// <summary>
/// Résultat d'une opération métier : soit une valeur, soit une erreur.
/// Évite d'utiliser des exceptions pour des cas prévus (identifiants invalides, etc.).
/// </summary>
public sealed class Result<T>
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

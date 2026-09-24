using System.Net;

namespace Api.Options;

/// <summary>
/// Proxy inverse placé devant l'API (Traefik dans Coolify), section « ReverseProxy ».
/// </summary>
public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    /// <summary>
    /// Réseaux (notation CIDR, ex. « 10.0.1.0/24 ») d'où peuvent venir les en-têtes
    /// X-Forwarded-For et X-Forwarded-Proto : ceux du proxy, et seulement eux.
    /// Vide (développement) : seul l'accès local (127.0.0.1, ::1) est de confiance.
    ///
    /// Sécurité : si on acceptait ces en-têtes de n'importe qui, un client pourrait
    /// s'inventer une adresse IP à chaque requête et contourner la limite de débit
    /// par IP sur la connexion.
    /// </summary>
    public string[] KnownNetworks { get; init; } = [];

    public static bool AreValid(ReverseProxyOptions options) =>
        options.KnownNetworks.All(network => IPNetwork.TryParse(network, out _));
}

namespace Api.Common;

public static class RateLimitPolicies
{
    // Endpoints d'authentification : limite les tentatives de force brute.
    public const string Auth = "auth";

    // Saisie d'un code d'invitation : empêche de tester des codes en masse.
    public const string JoinHousehold = "join-household";
}

namespace Api.Common;

public static class RateLimitPolicies
{
    // Endpoints d'authentification : limite les tentatives de force brute.
    public const string Auth = "auth";
}

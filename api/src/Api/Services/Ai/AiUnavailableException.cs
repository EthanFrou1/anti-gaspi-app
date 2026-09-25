namespace Api.Services.Ai;

/// <summary>
/// L'IA n'a pas pu fournir de réponse exploitable (panne, délai, refus, réponse coupée ou
/// invalide…). L'API répond 503 et l'appel n'est pas décompté du quota (recettes, tickets).
/// </summary>
public class AiUnavailableException(string reason, Exception? inner = null)
    : Exception(reason, inner);

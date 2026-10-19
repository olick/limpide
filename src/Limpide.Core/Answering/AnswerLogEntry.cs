namespace Limpide.Core.Answering;

/// <summary>
/// Une ligne du journal des réponses (table answer_log, ADR-018) : ce qui s'est passé pour une question, sans son
/// texte ni celui de la réponse, ni l'adresse du visiteur. Sert au coût par jour, à la dérive et à l'audit ;
/// <see cref="TraceId"/> mène à la trace détaillée tant que la supervision la garde.
/// </summary>
/// <param name="Outcome">Une des constantes de <see cref="AnswerOutcome"/>.</param>
/// <param name="Model">Null quand le modèle n'a pas été appelé (hors périmètre, quota, erreur avant la génération).</param>
/// <param name="BestScore">Similarité du meilleur passage ; null si aucune recherche n'a abouti.</param>
/// <param name="Error">Type de l'exception seulement : un message peut reprendre la question.</param>
public sealed record AnswerLogEntry(
    string Outcome,
    string? TraceId,
    int QuestionLength,
    string? SearchStrategy,
    string? Model,
    string? PromptVersion,
    int Passages,
    int Cited,
    double? BestScore,
    TimeSpan? Embedding,
    TimeSpan? Search,
    TimeSpan? Generation,
    TimeSpan? Total,
    long? InputTokens,
    long? OutputTokens,
    decimal? CostUsd,
    IReadOnlyList<string> Guardrails,
    string? Error = null)
{
    /// <summary>Durée de conservation, comme les retours des visiteurs ; les lignes plus anciennes sont supprimées.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(365);

    public static AnswerLogEntry From(AnswerResult result, string? traceId)
    {
        var outOfScope = result.Guardrails.Any(g => g.Code == AnswerService.OutOfScopeCode);
        return new AnswerLogEntry(
            outOfScope ? AnswerOutcome.OutOfScope : result.Declined ? AnswerOutcome.Declined : AnswerOutcome.Answered,
            traceId,
            result.Question.Length,
            result.SearchStrategy,
            outOfScope ? null : result.Model,
            result.PromptVersion,
            result.Passages.Count,
            result.CitedPassages.Count(),
            result.Passages.Count == 0 ? null : result.Passages.Max(p => p.Passage.Score),
            result.Timings.Embedding,
            result.Timings.Search,
            result.Timings.Generation,
            result.Timings.Total,
            result.Usage.InputTokens,
            result.Usage.OutputTokens,
            result.Usage.EstimatedCost,
            result.Guardrails.Select(g => g.Code).ToList());
    }

    /// <summary>Question refusée avant toute recherche (quota atteint) : rien n'a été appelé.</summary>
    public static AnswerLogEntry Refused(string outcome, int questionLength, string? traceId) =>
        new(outcome, traceId, questionLength, null, null, null, 0, 0, null, null, null, null, null, null, null, 0m, []);

    public static AnswerLogEntry Failed(Exception exception, int questionLength, TimeSpan elapsed, string? traceId) =>
        new(exception is OperationCanceledException ? AnswerOutcome.Timeout : AnswerOutcome.Error, traceId, questionLength,
            null, null, null, 0, 0, null, null, null, null, elapsed, null, null, null, [], exception.GetType().Name);
}

/// <summary>Issue d'une question, telle qu'enregistrée dans le journal (valeurs stables : on les compte).</summary>
public static class AnswerOutcome
{
    public const string Answered = "reponse";
    /// <summary>« Je ne sais pas » ou autre refus du modèle.</summary>
    public const string Declined = "je-ne-sais-pas";
    /// <summary>Sous le seuil de pertinence : le modèle n'est pas appelé.</summary>
    public const string OutOfScope = "hors-perimetre";
    public const string QuotaVisitor = "quota-visiteur";
    public const string QuotaDaily = "quota-journalier";
    public const string Timeout = "delai-depasse";
    public const string Error = "erreur";
}

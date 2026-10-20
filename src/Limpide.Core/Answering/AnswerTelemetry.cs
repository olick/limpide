using System.Diagnostics;

namespace Limpide.Core.Answering;

/// <summary>
/// Traces du pipeline de réponse (ADR-018) : une activité par question, une par recherche ; la vectorisation,
/// l'appel au modèle et les requêtes SQL s'y rattachent comme étapes filles. Sans abonné (supervision inactive),
/// <see cref="ActivitySource.StartActivity(string, ActivityKind)"/> renvoie null et rien n'est mesuré.
/// Jamais le texte de la question ni celui de la réponse : seulement des mesures et des codes.
/// </summary>
public static class AnswerTelemetry
{
    public const string SourceName = "Limpide.Answering";

    internal static readonly ActivitySource Source = new(SourceName);

    internal static void Describe(Activity? activity, AnswerResult result)
    {
        if (activity is null)
            return;

        var entry = AnswerLogEntry.From(result, traceId: null);
        activity.SetTag("limpide.outcome", entry.Outcome);
        activity.SetTag("limpide.search.strategy", result.SearchStrategy);
        activity.SetTag("limpide.prompt.version", result.PromptVersion);
        activity.SetTag("limpide.passages.count", entry.Passages);
        activity.SetTag("limpide.passages.cited", entry.Cited);
        activity.SetTag("limpide.score.best", entry.BestScore);
        // Noms des conventions de l'IA générative (gen_ai.*), comme l'étape fille de Microsoft.Extensions.AI.
        activity.SetTag("gen_ai.request.model", entry.Model);
        activity.SetTag("gen_ai.usage.input_tokens", entry.InputTokens);
        activity.SetTag("gen_ai.usage.output_tokens", entry.OutputTokens);
        activity.SetTag("limpide.cost.eur", entry.CostEur is { } cost ? (double)cost : null);
        activity.SetTag("limpide.guardrails", entry.Guardrails.ToArray());

        // Un événement par garde-fou : visible sur la ligne de temps de la trace. La description est omise, elle
        // peut citer la réponse.
        foreach (var guardrail in result.Guardrails)
            activity.AddEvent(new ActivityEvent("garde-fou", tags: new ActivityTagsCollection { ["limpide.guardrail"] = guardrail.Code }));
    }
}

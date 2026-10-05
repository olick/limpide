using Limpide.Core.Search;

namespace Limpide.Core.Answering;

/// <summary>
/// Tout ce que l'interface affiche pour une question : la réponse, les passages cités à l'identique,
/// et ce qui s'est passé sous le capot.
/// </summary>
/// <param name="Answer">Formulation de l'assistant, à présenter comme telle, distincte des textes cités.</param>
/// <param name="Declined">Le modèle a répondu qu'il ne savait pas (phrase <see cref="AnswerPrompt.Decline"/>).</param>
/// <param name="Passages">Passages fournis au modèle, dans l'ordre de la recherche, avec leur identifiant.</param>
public sealed record AnswerResult(
    string Question,
    string Answer,
    bool Declined,
    IReadOnlyList<SourcePassage> Passages,
    string SearchStrategy,
    string Model,
    string PromptVersion,
    AnswerTimings Timings,
    TokenUsage Usage,
    IReadOnlyList<Guardrail> Guardrails)
{
    public IEnumerable<SourcePassage> CitedPassages => Passages.Where(p => p.Cited);
}

/// <param name="Id">Identifiant dans le prompt et dans la réponse (« P1 »).</param>
public sealed record SourcePassage(string Id, RetrievedPassage Passage, bool Cited);

public sealed record AnswerTimings(TimeSpan Embedding, TimeSpan Search, TimeSpan Generation, TimeSpan Total);

/// <param name="EstimatedCost">En dollars, d'après le tarif configuré ; null si le tarif du modèle n'est pas connu.</param>
public sealed record TokenUsage(long? InputTokens, long? OutputTokens, decimal? EstimatedCost);

/// <summary>Un contrôle qui s'est déclenché sur cette réponse.</summary>
/// <param name="Code">Identifiant stable, pour compter les déclenchements (ex. « citation-inventee »).</param>
public sealed record Guardrail(string Code, string Description);

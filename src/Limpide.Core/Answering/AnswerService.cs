using System.Diagnostics;
using Limpide.Core.Search;
using Microsoft.Extensions.AI;

namespace Limpide.Core.Answering;

/// <summary>Tarif d'un modèle, en dollars par million de tokens (majoration d'inférence régionale comprise).</summary>
public sealed record ModelPrice(decimal InputPerMillion, decimal OutputPerMillion);

/// <param name="Model">Identifiant du modèle appelé, affiché et enregistré avec la réponse.</param>
/// <param name="Price">Null si le tarif n'est pas configuré : le coût est alors affiché comme inconnu, jamais deviné.</param>
public sealed record GenerationSettings(string Model, ModelPrice? Price, int PassageCount = 5, float Temperature = 0f, int MaxOutputTokens = 1000);

/// <summary>Question → recherche → génération → contrôles. Ne dépend que d'interfaces.</summary>
public sealed class AnswerService(IPassageSearch search, IChatClient chat, GenerationSettings settings)
{
    public async Task<AnswerResult> AskAsync(string question, CancellationToken ct)
    {
        var total = Stopwatch.StartNew();
        var found = await search.SearchAsync(question, settings.PassageCount, ct);

        var generation = Stopwatch.StartNew();
        var response = await chat.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, AnswerPrompt.System),
                new ChatMessage(ChatRole.User, AnswerPrompt.UserMessage(question, found.Passages)),
            ],
            new ChatOptions
            {
                ModelId = settings.Model,
                Temperature = settings.Temperature,
                MaxOutputTokens = settings.MaxOutputTokens,
            },
            ct);
        generation.Stop();

        var answer = response.Text.Trim();
        var declined = answer.Contains(AnswerPrompt.Decline, StringComparison.Ordinal);
        var citations = Citations.Check(answer, found.Passages.Count);

        var passages = found.Passages
            .Select((p, i) => new SourcePassage(AnswerPrompt.Id(i), p, citations.Cited.Contains(i + 1)))
            .ToList();

        return new AnswerResult(
            question,
            answer,
            declined,
            passages,
            search.Strategy,
            settings.Model,
            AnswerPrompt.Version,
            new AnswerTimings(found.EmbeddingDuration, found.QueryDuration, generation.Elapsed, total.Elapsed),
            Usage(response.Usage),
            Guardrails(answer, declined, citations, response.FinishReason));
    }

    private TokenUsage Usage(UsageDetails? usage)
    {
        var input = usage?.InputTokenCount;
        var output = usage?.OutputTokenCount;
        decimal? cost = settings.Price is { } price && input is not null && output is not null
            ? (input.Value * price.InputPerMillion + output.Value * price.OutputPerMillion) / 1_000_000m
            : null;
        return new TokenUsage(input, output, cost);
    }

    private static List<Guardrail> Guardrails(string answer, bool declined, CitationCheck citations, ChatFinishReason? finish)
    {
        var triggered = new List<Guardrail>();

        if (citations.Invented.Count > 0)
            triggered.Add(new Guardrail("citation-inventee",
                $"La réponse cite {string.Join(", ", citations.Invented)}, qui ne correspond à aucun passage fourni."));

        if (!declined && citations.Cited.Count == 0)
            triggered.Add(new Guardrail("sans-citation",
                "La réponse n'est rattachée à aucun passage : rien ne permet de la vérifier."));

        if (finish == ChatFinishReason.Length)
            triggered.Add(new Guardrail("reponse-tronquee",
                "La réponse a atteint la longueur maximale et peut être incomplète."));

        if (answer.Length == 0)
            triggered.Add(new Guardrail("reponse-vide", "Le modèle n'a rien répondu."));

        return triggered;
    }
}

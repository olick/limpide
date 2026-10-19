using System.Diagnostics;
using Limpide.Core.Search;
using Microsoft.Extensions.AI;

namespace Limpide.Core.Answering;

/// <summary>Tarif d'un modèle, en dollars par million de tokens (majoration d'inférence régionale comprise).</summary>
public sealed record ModelPrice(decimal InputPerMillion, decimal OutputPerMillion);

/// <param name="Model">Identifiant du modèle appelé, affiché et enregistré avec la réponse.</param>
/// <param name="Price">Null si le tarif n'est pas configuré : le coût est alors affiché comme inconnu, jamais deviné.</param>
/// <param name="MinScore">
/// Seuil de pertinence : si le meilleur passage a une similarité plus basse, le modèle n'est pas appelé.
/// Propre au modèle d'embedding et au texte vectorisé : à recalibrer si l'un change.
/// </param>
public sealed record GenerationSettings(
    string Model,
    ModelPrice? Price,
    int PassageCount = 5,
    float Temperature = 0f,
    int MaxOutputTokens = 1000,
    double MinScore = 0);

/// <summary>Question → recherche → génération → contrôles. Ne dépend que d'interfaces.</summary>
public sealed class AnswerService(IPassageSearch search, IChatClient chat, GenerationSettings settings, AnswerInstructions instructions)
{
    /// <summary>Garde-fou du seuil de pertinence : la question n'a pas été soumise au modèle.</summary>
    public const string OutOfScopeCode = "hors-perimetre";

    public async Task<AnswerResult> AskAsync(string question, CancellationToken ct)
    {
        using var activity = AnswerTelemetry.Source.StartActivity("Réponse à une question");
        activity?.SetTag("limpide.question.length", question.Length);
        try
        {
            var result = await AnswerAsync(question, ct);
            AnswerTelemetry.Describe(activity, result);
            return result;
        }
        catch (Exception ex) when (activity is not null)
        {
            // Le type seulement : le message d'une exception peut reprendre la question (pas d'AddException).
            activity.SetTag("error.type", ex.GetType().FullName);
            activity.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            throw;
        }
    }

    private async Task<AnswerResult> AnswerAsync(string question, CancellationToken ct)
    {
        var total = Stopwatch.StartNew();
        SearchOutcome found;
        // Étape « recherche » : la vectorisation de la question et la requête SQL s'y rattachent.
        using (AnswerTelemetry.Source.StartActivity("Recherche des passages"))
            found = await search.SearchAsync(question, settings.PassageCount, ct);

        var best = found.Passages.Count == 0 ? 0 : found.Passages.Max(p => p.Score);
        if (best < settings.MinScore)
            return OutOfScope(question, found, best, total.Elapsed);

        var generation = Stopwatch.StartNew();
        var response = await chat.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, instructions.System),
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

        var answer = ModelText.Clean(response.Text);
        // Refus = la phrase seule (guillemets éventuels mis à part). Une réponse sur le fond suivie de la phrase
        // de refus n'est pas un refus : elle est signalée (« reponse-ambigue »).
        var mentionsDecline = answer.Contains(AnswerPrompt.Decline, StringComparison.Ordinal);
        var citations = Citations.Check(answer, found.Passages.Count);
        // Refus : la phrase prévue, seule ; ou une autre formule de refus, courte et sans citation (Refusals).
        var declined = (mentionsDecline && answer.Trim('«', '»', '"', ' ', '\n').Length <= AnswerPrompt.Decline.Length + 2)
            || Refusals.IsRefusal(answer, citations.Cited.Count);

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
            instructions.Version,
            new AnswerTimings(found.EmbeddingDuration, found.QueryDuration, generation.Elapsed, total.Elapsed),
            Usage(response.Usage),
            Guardrails(answer, declined, mentionsDecline, citations, response.FinishReason));
    }

    /// <summary>Aucun passage assez proche : réponse fixe, sans appel au modèle (coût nul, aucun risque d'invention).</summary>
    private AnswerResult OutOfScope(string question, SearchOutcome found, double best, TimeSpan elapsed) => new(
        question,
        AnswerPrompt.OutOfScope,
        Declined: true,
        found.Passages.Select((p, i) => new SourcePassage(AnswerPrompt.Id(i), p, Cited: false)).ToList(),
        search.Strategy,
        Model: "(non appelé)",
        instructions.Version,
        new AnswerTimings(found.EmbeddingDuration, found.QueryDuration, TimeSpan.Zero, elapsed),
        new TokenUsage(0, 0, 0m),
        [new Guardrail(OutOfScopeCode,
            $"Meilleur passage à {best:F3}, sous le seuil de pertinence ({settings.MinScore:F2}) : le modèle n'est pas appelé.")]);

    private TokenUsage Usage(UsageDetails? usage)
    {
        var input = usage?.InputTokenCount;
        var output = usage?.OutputTokenCount;
        decimal? cost = settings.Price is { } price && input is not null && output is not null
            ? (input.Value * price.InputPerMillion + output.Value * price.OutputPerMillion) / 1_000_000m
            : null;
        return new TokenUsage(input, output, cost);
    }

    private List<Guardrail> Guardrails(
        string answer, bool declined, bool mentionsDecline, CitationCheck citations, ChatFinishReason? finish)
    {
        var triggered = new List<Guardrail>();

        if (mentionsDecline && !declined)
            triggered.Add(new Guardrail("reponse-ambigue",
                "La réponse donne des éléments puis déclare ne pas savoir : à lire avec prudence."));

        if (PromptLeak.Find(answer, instructions.System) is { } leak)
            triggered.Add(new Guardrail("consigne-divulguee",
                $"La réponse reprend un passage des consignes de l'assistant : « {leak} »."));

        if (LegalQualification.Find(answer) is { } verdict)
            triggered.Add(new Guardrail("qualification-juridique",
                $"La réponse semble trancher la situation de la personne : « {Shorten(verdict, 160)} »"));

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

    private static string Shorten(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}

using Limpide.Core.Answering;

namespace Limpide.Ingestion.Search;

/// <summary>
/// Pose une question : recherche, génération, contrôles. Affiche la réponse de l'assistant, les passages cités
/// reproduits à l'identique avec leur source et leur licence, puis ce qui s'est passé sous le capot.
/// </summary>
public sealed class AskCommand(AnswerService service)
{
    public async Task<int> RunAsync(string question, CancellationToken ct)
    {
        var result = await service.AskAsync(question, ct);

        Console.WriteLine($"Question : {result.Question}\n");
        Console.WriteLine("Réponse de l'assistant (formulation générée, à vérifier dans les textes cités) :\n");
        Console.WriteLine(result.Answer);

        var cited = result.CitedPassages.ToList();
        if (cited.Count > 0)
        {
            Console.WriteLine("\nTextes cités (reproduits à l'identique) :");
            foreach (var (id, p, _) in cited)
            {
                Console.WriteLine($"\n[{id}] {p.Heading}{(p.Anchor is null ? "" : $" ({p.Anchor})")} — score {p.Score:F3}");
                Console.WriteLine($"Source : {p.SourceName}, {p.DocumentTitle}, {p.Url}, collecté le {p.FetchedAt:yyyy-MM-dd}");
                Console.WriteLine($"Licence : {p.ReuseTerms}");
                Console.WriteLine(Indent(p.Content));
            }
        }

        var uncited = result.Passages.Where(p => !p.Cited).Select(p => p.Id).ToList();
        var (t, u) = (result.Timings, result.Usage);
        Console.WriteLine("\nSous le capot :");
        Console.WriteLine($"  Recherche   : {result.SearchStrategy}, {result.Passages.Count} passages fournis"
            + (uncited.Count > 0 ? $" (non cités : {string.Join(", ", uncited)})" : ""));
        Console.WriteLine($"  Modèle      : {result.Model}, consignes {result.PromptVersion}");
        Console.WriteLine($"  Durées      : vectorisation {Ms(t.Embedding)}, recherche {Ms(t.Search)}, "
            + $"génération {Ms(t.Generation)}, total {Ms(t.Total)}");
        Console.WriteLine($"  Tokens      : {u.InputTokens?.ToString() ?? "?"} en entrée, {u.OutputTokens?.ToString() ?? "?"} en sortie");
        Console.WriteLine($"  Coût estimé : {(u.EstimatedCost is { } cost ? $"{cost:0.000000} $" : "inconnu (tarif du modèle non configuré)")}");
        Console.WriteLine(result.Guardrails.Count == 0
            ? "  Garde-fous  : aucun déclenché"
            : "  Garde-fous  :\n" + string.Join("\n", result.Guardrails.Select(g => $"    - {g.Code} : {g.Description}")));

        return 0;
    }

    private static string Ms(TimeSpan duration) => $"{duration.TotalMilliseconds:N0} ms";

    private static string Indent(string text) => "  " + text.ReplaceLineEndings("\n  ");
}

using Limpide.Infrastructure.Storage;

namespace Limpide.Ingestion.Search;

/// <summary>
/// Affiche les retours des visiteurs en Markdown, les plus récents d'abord
/// (redirigeable : <c>feedback &gt; retours.md</c>).
/// </summary>
public sealed class FeedbackCommand(FeedbackStore store)
{
    private const int Limit = 200;

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var all = await store.ListAsync(Limit, ct);
        Console.WriteLine($"# Retours des visiteurs ({all.Count}{(all.Count == Limit ? $", les {Limit} plus récents" : "")})");

        foreach (var (createdAt, f) in all)
        {
            Console.WriteLine($"\n## {createdAt.ToLocalTime():yyyy-MM-dd HH:mm}\n");
            Write("Compris de Limpide", f.Understood);
            Write("Réponse fausse ou trompeuse", f.Misleading);
            Write("Panneau « sous le capot »", f.UnderTheHood);
            if (f.Context is { } c)
            {
                Console.WriteLine($"- **Question jointe** : {c.Question}");
                Console.WriteLine($"- **Produite par** : {c.Model}, consignes {c.PromptVersion} ; passages cités : {string.Join(" ; ", c.Cited)}");
                Console.WriteLine();
                Console.WriteLine(string.Join("\n", c.Answer.Split('\n').Select(l => $"> {l}")));
            }
        }

        return 0;
    }

    private static void Write(string label, string? text)
    {
        if (text is not null)
            Console.WriteLine($"- **{label}** : {text.ReplaceLineEndings(" ")}");
    }
}

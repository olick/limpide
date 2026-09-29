using Limpide.Ingestion.Storage;
using Microsoft.Extensions.AI;

namespace Limpide.Ingestion.Search;

/// <summary>Affiche les 5 passages les plus proches d'une question, avec leur score et leur source.</summary>
public sealed class SearchCommand(
    IEmbeddingGenerator<string, Embedding<float>> generator,
    SearchStore store,
    EmbeddingOptions options)
{
    public const int Limit = 5;

    public async Task<int> RunAsync(string question, CancellationToken ct)
    {
        var query = await generator.GenerateVectorAsync(question, cancellationToken: ct);
        var results = await store.SearchAsync(query, options.Label, Limit, ct);

        Console.WriteLine($"Question : {question}\n");
        foreach (var (result, rank) in results.Select((r, i) => (r, i + 1)))
        {
            var excerpt = result.Content.ReplaceLineEndings(" ");
            Console.WriteLine($"{rank}. [{result.Score:F3}] {result.Heading}{(result.Anchor is null ? "" : $" ({result.Anchor})")}");
            Console.WriteLine($"   {result.SourceName} — {result.DocumentTitle}, collecté le {result.FetchedAt:yyyy-MM-dd}");
            Console.WriteLine($"   {(excerpt.Length <= 200 ? excerpt : excerpt[..200] + "…")}\n");
        }

        return 0;
    }
}

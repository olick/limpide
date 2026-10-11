using Limpide.Core.Search;
using Limpide.Infrastructure.Storage;

namespace Limpide.Ingestion.Search;

/// <summary>
/// Lignage (S5) : pour une question, les passages que la recherche fournirait au modèle, et pour chacun d'où il vient —
/// version du document (empreinte, statut, dates), fichier brut collecté et texte extrait (présents ou non sur ce
/// poste), extracteur, découpeur, modèle d'embedding, contrôles qualité de la version. Sans appel au LLM.
/// </summary>
public sealed class TraceCommand(IPassageSearch search, ChunkStore chunks, IngestionOptions options)
{
    public async Task<int> RunAsync(string question, CancellationToken ct)
    {
        var outcome = await search.SearchAsync(question, 5, ct);
        var lineage = (await chunks.LineageAsync(outcome.Passages.Select(p => p.Origin.ChunkId).ToList(), ct))
            .ToDictionary(l => l.ChunkId);

        Console.WriteLine($"Question : {question}\nRecherche : {search.Strategy}\n");
        for (var i = 0; i < outcome.Passages.Count; i++)
        {
            var passage = outcome.Passages[i];
            Console.WriteLine($"P{i + 1}  [{passage.Score:0.000}] {passage.Heading}");
            if (!lineage.TryGetValue(passage.Origin.ChunkId, out var l))
            {
                Console.WriteLine("    lignage introuvable (passage supprimé depuis la recherche ?)\n");
                continue;
            }

            var extracted = Path.ChangeExtension(l.RawPath, ".json");
            Console.WriteLine($"    passage   {l.ChunkId} (n° {l.Ordinal} de la version)");
            Console.WriteLine($"    document  {passage.SourceName} / {passage.DocumentTitle} — {l.Url}");
            Console.WriteLine($"    version   {l.VersionHash} ({l.Status})");
            Console.WriteLine($"              collectée {Date(l.FetchedAt)}, publiée {(l.PublishedAt is { } p ? Date(p) : "—")}");
            Console.WriteLine($"    brut      {Path.Combine(options.RawDataPath, l.RawPath)} {Present(options.RawDataPath, l.RawPath)}");
            Console.WriteLine($"    extrait   {Path.Combine(options.ExtractedDataPath, extracted)} {Present(options.ExtractedDataPath, extracted)}");
            Console.WriteLine($"    produit   extracteur {l.ExtractorVersion}, découpeur {l.ChunkerVersion}, embedding {l.EmbeddingModel ?? "—"}");
            Console.WriteLine($"    qualité   {l.QualityPassed switch { true => "contrôles réussis", false => "contrôles en échec", null => "pas de rapport" }}\n");
        }

        return 0;
    }

    private static string Date(DateTimeOffset date) => date.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") + " UTC";

    private static string Present(string root, string path) =>
        File.Exists(Path.Combine(root, path)) ? "(présent)" : "(absent de ce poste)";
}

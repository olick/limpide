using Limpide.Core.Chunking;
using Limpide.Core.Corpus;
using Limpide.Core.Extraction;
using Limpide.Infrastructure.Storage;
using Limpide.Ingestion.Cli;
using Microsoft.Extensions.Logging;

namespace Limpide.Ingestion.Chunking;

/// <summary>
/// Découpe le texte extrait de chaque version courante en passages, de data/extracted vers la table chunks.
/// Ne refait rien si les passages existent déjà avec les mêmes versions d'extracteur et de découpeur.
/// </summary>
public sealed class ChunkCommand(
    DocumentVersionStore versions,
    ChunkStore chunks,
    IngestionOptions options,
    CommandSummary summary,
    ILogger<ChunkCommand> logger)
{
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var current = await versions.ListCurrentAsync(ct);
        var failures = 0;

        foreach (var version in current)
        {
            try
            {
                await ChunkAsync(version, ct);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException)
            {
                failures++;
                summary.Add("failed");
                logger.LogError("{Source} / {Title} : échec — {Message}", version.SourceName, version.DocumentTitle, ex.Message);
            }
        }

        logger.LogInformation("Découpage terminé : {Count} document(s), {Failures} échec(s)", current.Count, failures);
        return failures == 0 ? 0 : 1;
    }

    private async Task ChunkAsync(CurrentVersion version, CancellationToken ct)
    {
        var extracted = await ReadExtractedAsync(version, ct);
        var chunker = Chunkers.ForSource(Slugs.From(version.SourceName));

        if (await chunks.IsUpToDateAsync(version.Id, extracted.Extractor, chunker.Version, ct))
        {
            summary.Add("unchanged");
            logger.LogInformation("{Source} / {Title} : inchangé ({Chunker})", version.SourceName, version.DocumentTitle, chunker.Version);
            return;
        }

        var passages = chunker.Split(extracted.Blocks);
        if (passages.Count == 0)
            throw new InvalidDataException("aucun passage produit");

        await chunks.ReplaceAsync(version.Id, passages, extracted.Extractor, chunker.Version, ct);
        summary.Add("chunked");
        summary.Add("passages", passages.Count);

        var lengths = passages.Select(p => p.Content.Length).Order().ToList();
        logger.LogInformation(
            "{Source} / {Title} : {Count} passages ({Chunker}) — longueur min {Min}, médiane {Median}, max {Max} ; "
            + "{Short} sous {MinLength}, {Long} au-dessus de {MaxLength}",
            version.SourceName, version.DocumentTitle, passages.Count, chunker.Version,
            lengths[0], lengths[lengths.Count / 2], lengths[^1],
            lengths.Count(l => l < Packer.MinLength), Packer.MinLength,
            lengths.Count(l => l > Packer.MaxLength), Packer.MaxLength);
    }

    private async Task<ExtractedDocument> ReadExtractedAsync(CurrentVersion version, CancellationToken ct)
    {
        var path = Path.Combine(options.ExtractedDataPath, Path.ChangeExtension(version.RawPath, ".json"));
        if (!File.Exists(path))
            throw new InvalidDataException($"texte extrait absent : {path} (relancer extract)");

        var extracted = ExtractedDocumentJson.Deserialize(await File.ReadAllTextAsync(path, ct));
        if (extracted.ContentHash != version.ContentHash)
            throw new InvalidDataException($"{path} ne correspond pas à la version courante (relancer extract)");

        return extracted;
    }
}

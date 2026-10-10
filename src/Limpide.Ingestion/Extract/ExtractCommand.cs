using Limpide.Core.Corpus;
using Limpide.Core.Extraction;
using Limpide.Infrastructure.Storage;
using Limpide.Ingestion.Cli;
using Microsoft.Extensions.Logging;

namespace Limpide.Ingestion.Extract;

/// <summary>
/// Extrait le texte structuré des versions publiées et candidates, de data/raw vers data/extracted.
/// Ne refait rien si le fichier extrait existe déjà avec la même version d'extracteur. Enregistre l'empreinte du texte,
/// puis écarte les candidates dont le texte est identique à celui de la version publiée (fausses nouvelles versions).
/// </summary>
public sealed class ExtractCommand(
    DocumentVersionStore store,
    IngestionOptions options,
    CommandSummary summary,
    ILogger<ExtractCommand> logger)
{
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var versions = await store.ListActiveAsync(ct);
        var failures = 0;

        foreach (var version in versions)
        {
            try
            {
                await ExtractAsync(version, ct);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException)
            {
                failures++;
                summary.Add("failed");
                logger.LogError("{Source} / {Title} : échec — {Message}", version.SourceName, version.DocumentTitle, ex.Message);
            }
        }

        foreach (var discarded in await store.DiscardIdenticalTextAsync(ct))
        {
            summary.Add("discarded");
            logger.LogInformation("{Source} / {Title} : candidate écartée, texte identique à la version publiée ({Hash}…)",
                discarded.SourceName, discarded.DocumentTitle, discarded.ContentHash[..12]);
        }

        logger.LogInformation("Extraction terminée : {Count} version(s), {Failures} échec(s)", versions.Count, failures);
        return failures == 0 ? 0 : 1;
    }

    private async Task ExtractAsync(ActiveVersion version, CancellationToken ct)
    {
        var extractor = Extractors.ForSource(Slugs.From(version.SourceName));
        var outputPath = Path.Combine(options.ExtractedDataPath, Path.ChangeExtension(version.RawPath, ".json"));

        if (File.Exists(outputPath))
        {
            var existing = ExtractedDocumentJson.Deserialize(await File.ReadAllTextAsync(outputPath, ct));
            if (existing.Extractor == extractor.Version)
            {
                await RecordAsync(version, existing.Blocks, ct);
                summary.Add("unchanged");
                logger.LogInformation("{Source} / {Title} : inchangé ({Extractor}, {Blocks} blocs)",
                    version.SourceName, version.DocumentTitle, extractor.Version, existing.Blocks.Count);
                return;
            }
        }

        var raw = await ReadRawAsync(version, ct);
        var blocks = extractor.Extract(new MemoryStream(raw));
        if (blocks.Count == 0)
            throw new InvalidDataException("aucun texte extrait");

        var json = ExtractedDocumentJson.Serialize(new ExtractedDocument(extractor.Version, version.ContentHash, blocks));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var tempPath = outputPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, ct);
        File.Move(tempPath, outputPath, overwrite: true);
        await RecordAsync(version, blocks, ct);
        summary.Add("extracted");

        logger.LogInformation("{Source} / {Title} : extrait ({Extractor}, {Blocks} blocs, {Chars:N0} caractères)",
            version.SourceName, version.DocumentTitle, extractor.Version, blocks.Count, blocks.Sum(b => b.Text.Length));
    }

    /// <summary>Empreinte du texte (comparée à celle de la version publiée), et candidate passée à « extracted ».</summary>
    private async Task RecordAsync(ActiveVersion version, IReadOnlyList<Block> blocks, CancellationToken ct)
    {
        await store.SetTextHashAsync(version.Id, TextFingerprint.Compute(blocks), ct);
        await store.AdvanceAsync(version.Id, VersionStatus.Collected, VersionStatus.Extracted, ct);
    }

    /// <summary>Relit le fichier brut et vérifie qu'il correspond toujours à l'empreinte enregistrée.</summary>
    private async Task<byte[]> ReadRawAsync(ActiveVersion version, CancellationToken ct)
    {
        var rawPath = Path.Combine(options.RawDataPath, version.RawPath);
        if (!File.Exists(rawPath))
            throw new InvalidDataException($"fichier brut absent : {rawPath} (relancer fetch)");

        var raw = await File.ReadAllBytesAsync(rawPath, ct);
        if (ContentHash.Compute(raw) != version.ContentHash)
            throw new InvalidDataException($"fichier brut altéré : {rawPath} ne correspond plus à son empreinte");

        return raw;
    }
}

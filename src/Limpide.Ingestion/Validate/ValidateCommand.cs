using System.Text.Json;
using Limpide.Core.Corpus;
using Limpide.Core.Extraction;
using Limpide.Core.Quality;
using Limpide.Infrastructure.Storage;
using Limpide.Ingestion.Cli;
using Microsoft.Extensions.Logging;

namespace Limpide.Ingestion.Validate;

/// <summary>
/// Contrôles qualité entre le découpage et la vectorisation (S5) : une candidate découpée passe en « validated »
/// (elle sera vectorisée puis publiée) ou en « quarantined » (bloquée ; la version publiée reste en service).
/// Le rapport est enregistré dans quality_report. La version publiée reçoit aussi un rapport, sans changer de statut.
/// Une quarantaine n'est pas un échec de la commande (code de sortie 0) : elle est comptée dans le résumé.
/// </summary>
public sealed class ValidateCommand(
    DocumentVersionStore versions,
    ChunkStore chunks,
    IngestionOptions ingestion,
    QualityOptions options,
    CommandSummary summary,
    ILogger<ValidateCommand> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var active = await versions.ListActiveAsync(ct);
        var failures = 0;
        summary.Add("validated", 0);
        summary.Add("quarantined", 0);

        foreach (var version in active)
        {
            // Candidate pas encore découpée, ou déjà contrôlée ; version publiée qui a déjà son rapport.
            if (version.IsPublished ? version.HasQualityReport : version.Status != VersionStatus.Chunked)
            {
                summary.Add("unchanged");
                continue;
            }

            try
            {
                await ValidateAsync(version, ct);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException)
            {
                failures++;
                summary.Add("failed");
                logger.LogError("{Source} / {Title} : échec — {Message}", version.SourceName, version.DocumentTitle, ex.Message);
            }
        }

        logger.LogInformation("Contrôles terminés : {Count} version(s), {Failures} échec(s)", active.Count, failures);
        return failures == 0 ? 0 : 1;
    }

    private async Task ValidateAsync(ActiveVersion version, CancellationToken ct)
    {
        var path = Path.Combine(ingestion.ExtractedDataPath, Path.ChangeExtension(version.RawPath, ".json"));
        if (!File.Exists(path))
            throw new InvalidDataException($"texte extrait absent : {path} (relancer extract)");
        var extracted = ExtractedDocumentJson.Deserialize(await File.ReadAllTextAsync(path, ct));

        var input = new QualityInput(
            Slugs.From(version.SourceName), extracted.Blocks,
            await chunks.ListTextsAsync(version.Id, ct),
            await chunks.PublishedCountAsync(version.Id, ct));
        var report = QualityValidator.Validate(input, options);

        var json = JsonSerializer.Serialize(new { validatedAt = DateTimeOffset.UtcNow, report.Passed, report.Checks }, Json);
        await versions.SaveQualityReportAsync(version.Id, json, report.Passed, report.Summary(), ct);

        if (version.IsPublished)
        {
            summary.Add("reported");
            logger.LogInformation("{Source} / {Title} : version publiée contrôlée — {Summary}",
                version.SourceName, version.DocumentTitle, report.Summary());
        }
        else if (report.Passed)
        {
            summary.Add("validated");
            logger.LogInformation("{Source} / {Title} : candidate validée ({Hash}…)",
                version.SourceName, version.DocumentTitle, version.ContentHash[..12]);
        }
        else
        {
            summary.Add("quarantined");
            logger.LogWarning("{Source} / {Title} : candidate EN QUARANTAINE ({Hash}…) — {Summary}",
                version.SourceName, version.DocumentTitle, version.ContentHash[..12], report.Summary());
        }
    }
}

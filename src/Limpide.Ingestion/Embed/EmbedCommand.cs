using System.Diagnostics;
using Limpide.Infrastructure;
using Limpide.Infrastructure.Storage;
using Limpide.Ingestion.Cli;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Limpide.Ingestion.Embed;

/// <summary>
/// Calcule les embeddings des passages des versions publiées et des candidates découpées qui n'en ont pas (ou qui
/// viennent d'un autre modèle), par lots, puis marque « embedded » les candidates entièrement vectorisées. Chaque lot est enregistré dès qu'il est calculé : une interruption ne perd que le lot en cours,
/// et une relance reprend là où on s'était arrêté.
/// </summary>
/// <remarks>Texte vectorisé : le contenu seul, ou « titre + contenu » avec <see cref="EmbeddingOptions.IncludeHeading"/>.</remarks>
public sealed class EmbedCommand(
    IEmbeddingGenerator<string, Embedding<float>> generator,
    EmbeddingStore store,
    DocumentVersionStore versions,
    EmbeddingOptions options,
    CommandSummary summary,
    ILogger<EmbedCommand> logger)
{
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var total = await store.CountPendingAsync(options.Label, ct);
        summary.Add("pending", total);
        summary.Add("embedded", 0);
        if (total == 0)
        {
            logger.LogInformation("Aucun passage à vectoriser ({Model})", options.Label);
            await MarkReadyAsync(ct);
            return 0;
        }

        logger.LogInformation("{Total} passage(s) à vectoriser avec {Model}, par lots de {BatchSize}",
            total, options.Label, options.BatchSize);

        var overall = Stopwatch.StartNew();
        TimeSpan? firstBatch = null;
        int done = 0, characters = 0;

        while (await store.NextBatchAsync(options.Label, options.BatchSize, ct) is { Count: > 0 } batch)
        {
            var watch = Stopwatch.StartNew();
            var embeddings = await generator.GenerateAsync(batch.Select(c => options.TextToEmbed(c.Heading, c.Content)), cancellationToken: ct);

            if (embeddings.Count != batch.Count)
                throw new InvalidOperationException($"{embeddings.Count} embedding(s) reçus pour {batch.Count} passage(s)");
            if (embeddings[0].Vector.Length != options.Dimensions)
                throw new InvalidOperationException(
                    $"{options.Model} produit {embeddings[0].Vector.Length} dimensions, la base en attend {options.Dimensions}");

            await store.SaveAsync(batch.Select((c, i) => (c.Id, embeddings[i].Vector)).ToList(), options.Label, ct);

            firstBatch ??= watch.Elapsed;
            done += batch.Count;
            summary.Add("embedded", batch.Count);
            characters += batch.Sum(c => c.Content.Length);
            logger.LogInformation("{Done}/{Total} — lot de {Count} en {Seconds:F1} s", done, total, batch.Count, watch.Elapsed.TotalSeconds);
        }

        // Le premier lot inclut le chargement du modèle en mémoire : on le sort de la moyenne.
        var elapsed = overall.Elapsed;
        var steady = done > options.BatchSize ? (elapsed - firstBatch!.Value) / (done - options.BatchSize) : elapsed / done;
        logger.LogInformation(
            "Vectorisation terminée : {Done} passages, {Characters:N0} caractères, {Total:F1} s au total "
            + "(premier lot, avec chargement du modèle : {First:F1} s), {PerChunk:F0} ms par passage en régime établi",
            done, characters, elapsed.TotalSeconds, firstBatch!.Value.TotalSeconds, steady.TotalMilliseconds);

        await MarkReadyAsync(ct);
        return 0;
    }

    /// <summary>Candidates dont tous les passages sont vectorisés : prêtes pour publish.</summary>
    private async Task MarkReadyAsync(CancellationToken ct)
    {
        summary.Add("ready", 0);
        foreach (var ready in await versions.MarkEmbeddedAsync(options.Label, ct))
        {
            summary.Add("ready");
            logger.LogInformation("{Source} / {Title} : candidate vectorisée, prête à publier ({Hash}…)",
                ready.SourceName, ready.DocumentTitle, ready.ContentHash[..12]);
        }
    }
}

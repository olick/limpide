using System.Diagnostics;
using Limpide.Ingestion.Storage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Limpide.Ingestion.Embed;

/// <summary>
/// Calcule les embeddings des passages des versions courantes qui n'en ont pas (ou qui viennent d'un autre modèle),
/// par lots. Chaque lot est enregistré dès qu'il est calculé : une interruption ne perd que le lot en cours,
/// et une relance reprend là où on s'était arrêté.
/// </summary>
/// <remarks>Texte vectorisé : le contenu seul, ou « titre + contenu » avec <see cref="EmbeddingOptions.IncludeHeading"/>.</remarks>
public sealed class EmbedCommand(
    IEmbeddingGenerator<string, Embedding<float>> generator,
    EmbeddingStore store,
    EmbeddingOptions options,
    ILogger<EmbedCommand> logger)
{
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var total = await store.CountPendingAsync(options.Label, ct);
        if (total == 0)
        {
            logger.LogInformation("Aucun passage à vectoriser ({Model})", options.Label);
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

        return 0;
    }
}

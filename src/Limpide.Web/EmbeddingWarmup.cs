using Microsoft.Extensions.AI;

namespace Limpide.Web;

/// <summary>
/// Au démarrage, vectorise une phrase factice pour charger le modèle d'embedding en mémoire :
/// sans cela, le premier visiteur attend le démarrage à froid d'Ollama (4,7 s mesurées en S2).
/// Un échec n'empêche pas l'application de démarrer : il est seulement journalisé.
/// </summary>
public sealed class EmbeddingWarmup(IEmbeddingGenerator<string, Embedding<float>> generator, ILogger<EmbeddingWarmup> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var started = DateTime.UtcNow;
            await generator.GenerateVectorAsync("préchauffage", cancellationToken: stoppingToken);
            logger.LogInformation("Modèle d'embedding chargé en {Seconds:F1} s", (DateTime.UtcNow - started).TotalSeconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Préchauffage du modèle d'embedding impossible : la première question sera plus lente");
        }
    }
}

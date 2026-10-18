using Microsoft.Extensions.AI;

namespace Limpide.Web;

/// <summary>
/// Au démarrage, vectorise une phrase factice : le premier visiteur n'attend ni le démarrage à froid d'Ollama
/// (4,7 s mesurées en S2), ni l'ouverture de la connexion à l'API (OVHcloud AI Endpoints, ADR-012), et une clé
/// absente ou refusée se voit dans les journaux dès le démarrage.
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

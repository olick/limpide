using Limpide.Infrastructure.Storage;
using Limpide.Ingestion.Cli;
using Microsoft.Extensions.Logging;

namespace Limpide.Ingestion.Publish;

/// <summary>
/// Met en service les candidates prêtes (entièrement vectorisées), toutes dans une transaction : pour chaque document,
/// la version publiée passe en « archived » et la candidate en « published ». La recherche ne lit que les versions
/// publiées : elle passe de l'ancienne à la nouvelle sans jamais rester sans passages. Relançable sans effet.
/// </summary>
public sealed class PublishCommand(DocumentVersionStore versions, CommandSummary summary, ILogger<PublishCommand> logger)
{
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var published = await versions.PublishAsync(ct);
        summary.Add("published", published.Count);

        foreach (var version in published)
        {
            logger.LogInformation("{Source} / {Title} : version publiée ({Hash}…)",
                version.SourceName, version.DocumentTitle, version.ContentHash[..12]);
        }

        if (published.Count == 0)
            logger.LogInformation("Aucune version à publier");
        return 0;
    }
}

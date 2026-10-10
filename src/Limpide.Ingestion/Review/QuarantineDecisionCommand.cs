using Limpide.Infrastructure.Storage;
using Limpide.Ingestion.Cli;
using Microsoft.Extensions.Logging;

namespace Limpide.Ingestion.Review;

/// <summary>
/// Décision humaine sur une version en quarantaine (S5) : release la lève (elle sera vectorisée puis publiée au
/// prochain passage d'embed et publish), reject la rejette définitivement (une collecte du même contenu sera ignorée).
/// Texte de la commande : « empreinte motif » ; le motif est obligatoire et gardé dans status_reason.
/// </summary>
public sealed class QuarantineDecisionCommand(DocumentVersionStore versions, CommandSummary summary, ILogger<QuarantineDecisionCommand> logger)
{
    public async Task<int> RunAsync(string text, bool release, CancellationToken ct)
    {
        var parts = text.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            Console.Error.WriteLine($"Usage : {(release ? "release" : "reject")} <empreinte> <motif> (motif obligatoire, voir versions)");
            return 2;
        }

        var change = await versions.DecideQuarantineAsync(parts[0], release, parts[1], ct);
        summary.Add(release ? "released" : "rejected");
        if (release)
        {
            logger.LogInformation("{Source} / {Title} : quarantaine levée ({Hash}…) — {Motive}. Publiée au prochain passage d'embed et publish.",
                change.SourceName, change.DocumentTitle, change.ContentHash[..12], parts[1]);
        }
        else
        {
            logger.LogInformation("{Source} / {Title} : version rejetée ({Hash}…) — {Motive}. Une collecte du même contenu sera ignorée.",
                change.SourceName, change.DocumentTitle, change.ContentHash[..12], parts[1]);
        }

        return 0;
    }
}

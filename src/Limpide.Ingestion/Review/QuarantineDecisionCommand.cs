using Limpide.Core.Administration;
using Limpide.Infrastructure.Storage;
using Limpide.Ingestion.Cli;
using Microsoft.Extensions.Logging;

namespace Limpide.Ingestion.Review;

/// <summary>
/// Décision humaine sur une version en quarantaine (S5) : release la lève (vectorisée puis publiée au prochain passage
/// d'embed et publish), retry la renvoie dans tout le circuit après une correction du code (extracteur, seuils), reject
/// la rejette définitivement (une collecte du même contenu sera ignorée).
/// Texte de la commande : « empreinte motif » ; le motif est obligatoire et gardé dans status_reason.
/// </summary>
public sealed class QuarantineDecisionCommand(DocumentVersionStore versions, CommandSummary summary, ILogger<QuarantineDecisionCommand> logger)
{
    public async Task<int> RunAsync(string text, QuarantineDecision decision, CancellationToken ct)
    {
        var parts = text.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            Console.Error.WriteLine($"Usage : {decision.ToString().ToLowerInvariant()} <empreinte> <motif> (motif obligatoire, voir versions)");
            return 2;
        }

        var change = await versions.DecideQuarantineAsync(parts[0], decision, parts[1], Actor.Console, ct);
        summary.Add(decision switch { QuarantineDecision.Release => "released", QuarantineDecision.Retry => "retried", _ => "rejected" });
        var next = decision switch
        {
            QuarantineDecision.Release => "quarantaine levée ; publiée au prochain passage d'embed et publish",
            QuarantineDecision.Retry => "renvoyée dans le circuit ; recontrôlée au prochain passage (extract → validate)",
            _ => "rejetée ; une collecte du même contenu sera ignorée",
        };
        logger.LogInformation("{Source} / {Title} ({Hash}…) : {Next} — {Motive}",
            change.SourceName, change.DocumentTitle, change.ContentHash[..12], next, parts[1]);

        return 0;
    }
}

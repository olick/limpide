using Limpide.Infrastructure.Storage;

namespace Limpide.Ingestion.Review;

/// <summary>
/// État des documents : version publiée, candidates en cours, versions en quarantaine (avec leur raison), et versions
/// écartées ces 7 derniers jours. Sert à trouver l'empreinte d'une version avant release ou reject.
/// </summary>
public sealed class VersionsCommand(DocumentVersionStore versions)
{
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var rows = await versions.ListForReviewAsync(ct);
        foreach (var document in rows.GroupBy(r => (r.SourceName, r.DocumentTitle)))
        {
            Console.WriteLine($"\n{document.Key.SourceName} / {document.Key.DocumentTitle}");
            foreach (var row in document)
            {
                var date = (row.PublishedAt ?? row.FetchedAt).ToUniversalTime().ToString("yyyy-MM-dd HH:mm");
                var reason = row.Reason is null ? "" : $" — {row.Reason}";
                Console.WriteLine($"  {row.ContentHash[..12]}  {row.Status,-11}  {date} UTC{reason}");
            }
        }

        Console.WriteLine("\nLever une quarantaine (après vérification du texte) : release <empreinte> <motif>");
        Console.WriteLine("La recontrôler après une correction du code       : retry <empreinte> <motif>");
        Console.WriteLine("La rejeter définitivement                         : reject <empreinte> <motif>");
        return 0;
    }
}

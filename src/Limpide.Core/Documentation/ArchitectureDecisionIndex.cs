using System.Globalization;
using System.Text.RegularExpressions;

namespace Limpide.Core.Documentation;

/// <summary>Une décision d'architecture : une ligne du tableau de <c>docs/adr/README.md</c>.</summary>
public sealed record ArchitectureDecision(string Number, string File, string Title, DateOnly Date);

/// <summary>Lecture de l'index des décisions d'architecture (<c>docs/adr/README.md</c>), un fichier tenu à la main.</summary>
public static partial class ArchitectureDecisionIndex
{
    /// <summary>
    /// Lignes « | [001](001-pgvector.md) | Décision | 2026-09-28 | » ; le reste du fichier est ignoré, comme une ligne
    /// dont la date n'existe pas (une faute de frappe ne doit pas faire tomber la page qui affiche l'index).
    /// </summary>
    public static IReadOnlyList<ArchitectureDecision> Parse(string markdown)
    {
        var decisions = new List<ArchitectureDecision>();
        foreach (Match m in Row().Matches(markdown))
        {
            if (!DateOnly.TryParseExact(m.Groups["date"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;
            decisions.Add(new ArchitectureDecision(
                m.Groups["number"].Value,
                m.Groups["file"].Value,
                m.Groups["title"].Value.Replace("`", "").Trim(),
                date));
        }
        return decisions;
    }

    [GeneratedRegex(@"^\|\s*\[(?<number>\d{3})\]\((?<file>[\w.-]+\.md)\)\s*\|\s*(?<title>[^|]+?)\s*\|\s*(?<date>\d{4}-\d{2}-\d{2})\s*\|\s*$", RegexOptions.Multiline)]
    private static partial Regex Row();
}

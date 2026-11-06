using System.Text.RegularExpressions;

namespace Limpide.Core.Evaluation;

/// <summary>
/// Dates, montants, pourcentages et durées d'un texte, sous une forme comparable (espaces insécables, gras Markdown,
/// « 1er », « € », « 35 millions d'euros »). Citer le bon article ne dit rien de ce que la réponse en tire : l'AI Act
/// modifié (ADR-023) et les 35 M€ écrits de mémoire (q06) passaient le contrôle et le juge.
/// </summary>
public static partial class AnswerFigures
{
    /// <summary>Chiffres de la réponse absents des passages cités : à relire, seule une personne peut conclure.</summary>
    public static IReadOnlyList<string> Unsourced(string answer, IEnumerable<string> citedPassages)
    {
        var sourced = citedPassages.SelectMany(Find).ToHashSet();
        return Find(answer).Where(figure => !sourced.Contains(figure)).ToList();
    }

    /// <summary>
    /// Le fait figure-t-il dans le texte ? « | » sépare les formulations acceptées (« 35 000 000|35 millions ») ; un
    /// chiffre n'en valide pas un autre plus long (« 7 % » ne se trouve pas dans « 17 % »).
    /// </summary>
    public static bool Contains(string text, string fact)
    {
        var normalized = Normalize(text);
        return fact.Split('|').Any(option =>
            Regex.IsMatch(normalized, $@"(?<![\d,]){Regex.Escape(Normalize(option))}(?!\d)"));
    }

    private static IEnumerable<string> Find(string text) =>
        Figure().Matches(Normalize(text)).Select(m => m.Value).Distinct();

    private static string Normalize(string text)
    {
        var plain = Spaces().Replace(text.Replace("*", ""), " ").Trim().ToLowerInvariant();
        plain = FirstOfMonth().Replace(plain, "1 ");
        plain = Euros().Replace(plain, "eur");
        plain = Millions().Replace(plain, "$1 000 000 eur");
        return Percent().Replace(plain, "$1 %");
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\b1er ")]
    private static partial Regex FirstOfMonth();

    [GeneratedRegex(@"€|\beuros?\b")]
    private static partial Regex Euros();

    [GeneratedRegex(@"(?<![\d,])(\d+) millions? d['’]eur\b")]
    private static partial Regex Millions();

    [GeneratedRegex(@"(\d) ?%")]
    private static partial Regex Percent();

    [GeneratedRegex(@"\b\d{1,2} (?:janvier|février|mars|avril|mai|juin|juillet|août|septembre|octobre|novembre|décembre) \d{4}\b"
        + @"|\b\d{1,3}(?: \d{3})* eur\b"
        + @"|\b\d+(?:,\d+)? %"
        + @"|\b\d+ (?:heures?|jours?|semaines?|mois|ans?)\b")]
    private static partial Regex Figure();
}

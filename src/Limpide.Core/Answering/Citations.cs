using System.Text.RegularExpressions;

namespace Limpide.Core.Answering;

/// <param name="Cited">Numéros (à partir de 1) des passages fournis et cités, dans l'ordre de première citation.</param>
/// <param name="Invented">Identifiants cités qui ne correspondent à aucun passage fourni (ex. « P7 » pour 5 passages).</param>
public sealed record CitationCheck(IReadOnlyList<int> Cited, IReadOnlyList<string> Invented);

/// <summary>Contrôle déterministe des citations d'une réponse, sans LLM.</summary>
public static partial class Citations
{
    /// <summary>
    /// Reconnaît [P1], [P1][P3] et les variantes que les modèles produisent : [P1, P3], [P1, 3], [p2], et un renvoi
    /// plus précis à l'intérieur du passage, [P1a], [P4h] (Small, Medium), [P5.1.a], [P4.c] (Medium) :
    /// c'est le passage cité qui compte. Chaque morceau séparé par « , » ou « ; » est lu sur son numéro de tête.
    /// </summary>
    public static CitationCheck Check(string answer, int passageCount)
    {
        var cited = new List<int>();
        var invented = new List<string>();

        foreach (Match group in Bracket().Matches(answer))
        {
            foreach (var part in group.Groups[1].Value.Split([',', ';']))
            {
                if (LeadingNumber().Match(part) is not { Success: true } number)
                    continue; // « [P1, point a] » : « point a » n'est pas un passage
                var n = int.Parse(number.Groups[1].Value);
                if (n >= 1 && n <= passageCount)
                {
                    if (!cited.Contains(n))
                        cited.Add(n);
                }
                else if (!invented.Contains($"P{n}"))
                {
                    invented.Add($"P{n}");
                }
            }
        }

        return new CitationCheck(cited, invented);
    }

    /// <summary>
    /// Remplace chaque citation par ce que rend <paramref name="link"/> (identifiant « P6 », texte affiché ; null pour
    /// laisser le texte tel quel). Mêmes règles de lecture que <see cref="Check"/> : ce que le contrôle compte comme une
    /// citation s'affiche comme une citation. Un seul passage dans les crochets : le lien porte sur tout le renvoi
    /// ([P6.3], [P1, paragraphe 4]) ; plusieurs : un lien chacun ([P1, P3]). Le texte peut être du HTML : une balise dans
    /// les crochets laisse la citation telle quelle, un lien la couperait ; une entité (« &amp;amp; ») n'est pas coupée.
    /// </summary>
    public static string Link(string text, Func<string, string, string?> link) =>
        Bracket().Replace(text, match =>
        {
            if (match.Value.Contains('<'))
                return match.Value;

            var pieces = Separator().Split(match.Groups[1].Value);
            var ids = pieces.Select(Id).OfType<string>().Distinct().ToList();
            if (ids.Count == 1)
                return link(ids[0], match.Value) ?? match.Value;

            return "[" + string.Concat(pieces.Select(piece =>
                Id(piece) is { } id && link(id, piece.Trim()) is { } linked ? piece.Replace(piece.Trim(), linked) : piece)) + "]";
        });

    /// <summary>« P01 » et « P1 » désignent le même passage, comme dans <see cref="Check"/>.</summary>
    private static string? Id(string piece) =>
        LeadingNumber().Match(piece) is { Success: true } number ? $"P{number.Groups[1].Value.TrimStart('0')}" : null;

    [GeneratedRegex(@"(,|(?<!&[a-zA-Z0-9#]+);)")]
    private static partial Regex Separator();

    /// <summary>Crochets qui commencent par un identifiant de passage : [P…], au plus 24 caractères.</summary>
    [GeneratedRegex(@"\[\s*([Pp]\s*\d+[^\[\]\n]{0,20})\]")]
    private static partial Regex Bracket();

    [GeneratedRegex(@"^\s*[Pp]?\s*(\d+)")]
    private static partial Regex LeadingNumber();
}

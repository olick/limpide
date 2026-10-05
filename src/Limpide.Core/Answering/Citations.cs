using System.Text.RegularExpressions;

namespace Limpide.Core.Answering;

/// <param name="Cited">Numéros (à partir de 1) des passages fournis et cités, dans l'ordre de première citation.</param>
/// <param name="Invented">Identifiants cités qui ne correspondent à aucun passage fourni (ex. « P7 » pour 5 passages).</param>
public sealed record CitationCheck(IReadOnlyList<int> Cited, IReadOnlyList<string> Invented);

/// <summary>Contrôle déterministe des citations d'une réponse, sans LLM.</summary>
public static partial class Citations
{
    /// <summary>
    /// Reconnaît [P1], [P1][P3] et les variantes que les modèles produisent parfois : [P1, P3], [P1, 3], [p2],
    /// et [P1a] ou [P4h] (lettre du point de l'article ajoutée par le modèle) : c'est bien le passage P1 qui est cité.
    /// </summary>
    public static CitationCheck Check(string answer, int passageCount)
    {
        var cited = new List<int>();
        var invented = new List<string>();

        foreach (Match group in Bracket().Matches(answer))
        {
            foreach (Match number in Number().Matches(group.Groups[1].Value))
            {
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

    [GeneratedRegex(@"\[\s*([Pp]\s*\d+[a-z]{0,4}(?:\s*[,;]\s*[Pp]?\s*\d+[a-z]{0,4})*)\s*\]")]
    private static partial Regex Bracket();

    [GeneratedRegex(@"(\d+)")]
    private static partial Regex Number();
}

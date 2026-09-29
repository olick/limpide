using System.Text.RegularExpressions;

namespace Limpide.Core.Extraction;

internal static partial class Text
{
    /// <summary>
    /// Espaces multiples, retours à la ligne et espaces insécables ramenés à une espace.
    /// Seule transformation appliquée au texte : elle ne touche ni aux mots ni à la ponctuation.
    /// </summary>
    public static string Normalize(string? value) =>
        value is null ? string.Empty : Whitespace().Replace(value, " ").Trim();

    [GeneratedRegex(@"[\s  ]+")]
    private static partial Regex Whitespace();
}

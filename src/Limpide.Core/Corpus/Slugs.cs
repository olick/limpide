using System.Globalization;
using System.Text;

namespace Limpide.Core.Corpus;

public static class Slugs
{
    /// <summary>Minuscules ASCII, chiffres et tirets : utilisable comme nom de dossier.</summary>
    public static string From(string value)
    {
        var builder = new StringBuilder(value.Length);
        // Décomposition Unicode : « é » devient « e » + accent, l'accent est ensuite ignoré.
        foreach (var c in value.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
                builder.Append(c);
            else if (builder.Length > 0 && builder[^1] != '-')
                builder.Append('-');
        }

        return builder.ToString().TrimEnd('-');
    }
}

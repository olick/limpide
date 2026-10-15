using System.Text.RegularExpressions;

namespace Limpide.Core.Answering;

/// <summary>
/// Fuite de la consigne système dans la réponse (tentative d'injection réussie) : la réponse reprend une suite de
/// mots de la consigne. Déterministe, sans IA : une suite de <see cref="WindowSize"/> mots identiques (casse et
/// ponctuation ignorées) ne se produit pas par hasard dans une réponse sur le fond.
/// </summary>
public static partial class PromptLeak
{
    public const int WindowSize = 8;

    /// <summary>Premier passage de la consigne <paramref name="system"/> retrouvé dans la réponse, ou null.</summary>
    public static string? Find(string answer, string system)
    {
        // La phrase de refus figure dans la consigne et doit pouvoir apparaître dans une réponse : on l'en retire.
        var systemWindows = Windows(system.Replace(AnswerPrompt.Decline, " ")).ToHashSet();
        return Windows(answer).FirstOrDefault(systemWindows.Contains);
    }

    private static IEnumerable<string> Windows(string text)
    {
        var words = Word().Matches(text.ToLowerInvariant().Replace('’', '\'')).Select(m => m.Value).ToArray();
        for (var i = 0; i + WindowSize <= words.Length; i++)
            yield return string.Join(' ', words, i, WindowSize);
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Word();
}

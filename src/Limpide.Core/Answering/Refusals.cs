namespace Limpide.Core.Answering;

/// <summary>
/// Refus formulés autrement que la phrase prévue (<see cref="AnswerPrompt.Decline"/>). Mesure du 2026-10-15 : face à
/// une tentative d'injection (« ignore tes instructions… »), Mistral Medium répond « Je ne peux pas répondre à cette
/// demande. » : un refus correct, que seule la phrase exacte faisait reconnaître, d'où un garde-fou « sans-citation »
/// et un échec d'évaluation à tort.
/// </summary>
public static class Refusals
{
    private static readonly string[] Openings =
    [
        "je ne sais pas",
        "je ne peux pas",
        "je ne suis pas en mesure",
        "je ne peux répondre",
        "désolé, je ne peux",
        "je suis désolé, je ne peux",
    ];

    /// <summary>
    /// Réponse courte, sans citation, qui commence par une formule de refus. Une réponse longue qui commence par
    /// « je ne peux pas… » puis répond sur le fond n'est pas un refus.
    /// </summary>
    public static bool IsRefusal(string answer, int citedCount)
    {
        var text = answer.Trim('«', '»', '"', ' ', '\n').ToLowerInvariant().Replace('’', '\'');
        return citedCount == 0 && text.Length <= 200 && Openings.Any(text.StartsWith);
    }
}

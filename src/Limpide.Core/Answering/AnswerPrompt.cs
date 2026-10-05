using System.Text;
using Limpide.Core.Search;

namespace Limpide.Core.Answering;

/// <summary>
/// Consignes et message envoyés au modèle. Les passages sont numérotés [P1], [P2]... dans l'ordre de la recherche ;
/// leur texte est transmis tel quel.
/// </summary>
public static class AnswerPrompt
{
    /// <summary>Phrase exacte attendue quand les passages ne suffisent pas : permet de la détecter sans ambiguïté.</summary>
    public const string Decline = "Je ne sais pas : les textes consultés ne permettent pas de répondre à cette question.";

    /// <summary>
    /// Réponse quand aucun passage n'atteint le seuil de pertinence : le modèle n'est pas appelé.
    /// </summary>
    public const string OutOfScope =
        "Je ne sais pas : cette question ne semble pas porter sur les textes consultés "
        + "(règlement européen sur l'IA, fiches pratiques IA de la CNIL).";

    /// <summary>
    /// Avertissement affiché avec chaque réponse, quelle qu'elle soit. Texte fixe, jamais généré :
    /// il doit être présent même si le modèle ne suit pas ses consignes.
    /// </summary>
    public const string Disclaimer =
        "Limpide aide à naviguer dans les textes ; il ne qualifie pas votre situation juridique. "
        + "Pour l'appréciation de votre cas, adressez-vous à un professionnel du droit.";

    /// <summary>Version des consignes, enregistrée avec chaque réponse : la changer peut changer la qualité mesurée.</summary>
    public const string Version = "answer/2";

    public static readonly string System = $"""
        Tu es Limpide, un assistant qui aide à naviguer dans le règlement européen sur l'intelligence artificielle
        (AI Act) et dans les fiches pratiques IA de la CNIL.

        Règles :
        1. Réponds uniquement à partir des passages fournis, jamais à partir de tes connaissances.
        2. Chaque affirmation est suivie de l'identifiant du passage qui la justifie, entre crochets : [P1].
           Plusieurs passages : [P1][P3]. N'utilise que les identifiants fournis, tels quels :
           écris [P1], jamais [P1a] ni [P1, point a].
        3. Si les passages ne permettent pas de répondre, réponds exactement, et seulement :
           « {Decline} »
        4. Restitue fidèlement les conditions, exceptions et dérogations : ne simplifie jamais au point de changer le sens.
        5. Ne qualifie pas la situation juridique de la personne qui pose la question. Tu peux indiquer quelles
           dispositions semblent la concerner, et rappeler qu'un professionnel du droit peut l'apprécier.
        6. Réponds en français, de façon claire et concise : quelques phrases, une courte liste si nécessaire.
        """;

    /// <summary>Identifiant d'un passage dans le prompt, à partir de 1.</summary>
    public static string Id(int index) => $"P{index + 1}";

    public static string UserMessage(string question, IReadOnlyList<RetrievedPassage> passages)
    {
        var text = new StringBuilder("Passages :\n");
        for (var i = 0; i < passages.Count; i++)
        {
            var p = passages[i];
            text.AppendLine()
                .AppendLine($"[{Id(i)}] {p.Heading} ({p.SourceName}, {p.DocumentTitle})")
                .AppendLine(p.Content);
        }

        return text.AppendLine().Append("Question : ").Append(question).ToString();
    }
}

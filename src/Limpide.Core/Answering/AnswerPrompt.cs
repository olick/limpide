using System.Text;
using Limpide.Core.Search;

namespace Limpide.Core.Answering;

/// <summary>
/// Message envoyé au modèle et textes fixes (refus, hors sujet, avertissement). Les consignes système sont dans un
/// fichier versionné (src/prompts/answer/N.txt, <see cref="AnswerInstructions"/>). Les passages sont numérotés [P1],
/// [P2]... dans l'ordre de la recherche ; leur texte est transmis tel quel.
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
    /// Réponse quand la question ressemble à une tentative d'injection (<see cref="PromptInjection"/>) : ni recherche,
    /// ni modèle.
    /// </summary>
    public const string InjectionRefused =
        "Je ne peux pas répondre à cette demande : elle s'adresse aux consignes de l'assistant plutôt qu'aux textes. "
        + "Posez votre question sur le règlement européen sur l'IA ou les fiches pratiques IA de la CNIL.";

    /// <summary>
    /// Avertissement affiché avec chaque réponse, quelle qu'elle soit. Texte fixe, jamais généré :
    /// il doit être présent même si le modèle ne suit pas ses consignes.
    /// </summary>
    public const string Disclaimer =
        "Limpide aide à naviguer dans les textes ; il ne qualifie pas votre situation juridique. "
        + "Pour l'appréciation de votre cas, adressez-vous à un professionnel du droit.";

    /// <summary>Repère de la phrase de refus dans le fichier des consignes, remplacé par <see cref="Decline"/>.</summary>
    public const string DeclinePlaceholder = "{{refus}}";

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

/// <summary>
/// Consignes système de la rédaction, lues par l'infrastructure dans src/prompts/answer/N.txt (S7) : modifier le
/// texte, c'est créer un nouveau fichier, donc une nouvelle version, enregistrée avec chaque réponse et comparée par
/// l'évaluation.
/// </summary>
/// <param name="Version">« answer/3 » : chemin du fichier sans extension.</param>
public sealed record AnswerInstructions(string Version, string System)
{
    /// <summary>
    /// La phrase de refus vient du code (sa détection en dépend) : le fichier la désigne par
    /// <see cref="AnswerPrompt.DeclinePlaceholder"/>, et un fichier qui ne la contient pas est refusé au démarrage.
    /// </summary>
    public static AnswerInstructions FromTemplate(string version, string template)
    {
        if (!template.Contains(AnswerPrompt.DeclinePlaceholder, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Consignes {version} : repère {AnswerPrompt.DeclinePlaceholder} absent ; sans lui, les refus ne seraient plus reconnus.");
        return new AnswerInstructions(version, template.TrimEnd().Replace(AnswerPrompt.DeclinePlaceholder, AnswerPrompt.Decline));
    }
}

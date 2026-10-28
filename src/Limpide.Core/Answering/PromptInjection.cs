using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Limpide.Core.Answering;

/// <summary>
/// Tentative d'injection dans la question (S8, garde-fous v2) : la question s'adresse aux consignes de l'assistant
/// plutôt qu'aux textes. Déterministe, sans IA ni service tiers (la question ne quitte pas le serveur pour ce
/// contrôle). Une question repérée n'est ni recherchée ni transmise au modèle : réponse fixe
/// (<see cref="AnswerPrompt.InjectionRefused"/>).
/// </summary>
/// <remarks>
/// Règles volontairement étroites : un refus à tort d'une vraie question coûte plus cher qu'une injection laissée au
/// modèle, qui reste la seconde ligne de défense (il a refusé toutes celles du jeu/2). Chaque règle vise une technique
/// connue, pas une question du jeu d'évaluation : une injection formulée autrement passe (faux texte de loi cité dans
/// la question, par exemple, i07). Les formulations légitimes proches (« quel rôle joue un déployeur », « si mon
/// entreprise ignore les règles », « vos règles de confidentialité ») sont des cas de test qui ne doivent pas être
/// repérés. Le texte est comparé sans accents ni majuscules.
/// </remarks>
public static partial class PromptInjection
{
    /// <summary>La technique repérée (libellé court, sans reprendre la question), ou null.</summary>
    public static string? Find(string question)
    {
        var text = Normalize(question);
        if (OwnInstructions().IsMatch(text))
            return "la question s'adresse aux consignes de l'assistant";
        if (RoleChange().IsMatch(text))
            return "la question demande à l'assistant de changer de rôle";
        if (FakeDelimiter().IsMatch(text))
            return "la question imite une consigne système ou un séparateur";
        if (PassageMarker().IsMatch(text))
            return "la question contient un renvoi [P…] que l'assistant n'a pas fourni";
        return null;
    }

    /// <summary>Minuscules, sans accents, apostrophe droite : « Règles » et « regles » se comparent pareil.</summary>
    private static string Normalize(string question)
    {
        var decomposed = question.ToLowerInvariant().Replace('’', '\'').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        return builder.ToString();
    }

    // « tes instructions », « consigne système », « les règles que tu dois suivre », « ignore all previous
    // instructions ». « Règles » seul n'y figure pas : « vos règles de confidentialité » est une vraie question.
    [GeneratedRegex(
        @"\b(tes|vos|ta|votre)\s+(propres\s+)?(instructions?|consignes?|directives?|prompt)\b"
        + @"|\b(instructions?|consignes?|directives?)\s+(precedentes?|ci-dessus|anterieures?|initiales?|de depart)\b"
        + @"|\b(consignes?|instructions?|prompt|message)\s+systemes?\b"
        + @"|\b(regles?|consignes?|instructions?)\s+(que|qui)\s+(tu|vous)\s+(dois|devez|suis|suivez|as recues?|avez recues?|appliques?|appliquez)\b"
        + @"|\b(ignore|disregard|forget|override)\s+(all\s+)?(the\s+|your\s+)?(previous|prior|above|earlier|your|all)\s+(\w+\s+)?(instructions|rules|prompts?|guidelines)\b"
        + @"|\bsystem\s+prompt\b|\byour\s+(instructions|guidelines|system\s+prompt)\b")]
    private static partial Regex OwnInstructions();

    // Changement de rôle en début de phrase : « joue un assistant » mais pas « quel rôle joue un déployeur ».
    [GeneratedRegex(
        @"\btu es (desormais|maintenant|a present|dorenavant)\b"
        + @"|(^|[.:;!?\n]\s*|,\s*)(joue|jouez|incarne|incarnez)\s+(le role|un|une)\b"
        + @"|\b(fais|faites) comme si (tu|vous)\b|\bmode (developpeur|dan|debug)\b"
        + @"|\byou are now\b|\bact as\b|\bpretend (to be|you)\b|\bjailbreak")]
    private static partial Regex RoleChange();

    // Séparateurs et rôles de conversation imités pour faire croire que la consigne est finie ou remplacée.
    [GeneratedRegex(
        @"#{3,}|\[/?inst\]|<\|"
        + @"|<\s*/?\s*(system|systeme|instructions?|passages?|consignes?)\s*>"
        + @"|(^|\n)\s*(system|systeme|assistant)\s*:"
        + @"|\b(fin|debut) des (passages|instructions|consignes|textes)\b"
        + @"|\bnouvelles? consignes?\b|\bnouvelles? instructions?\s*:")]
    private static partial Regex FakeDelimiter();

    // [P1], [P2a] : seuls les passages fournis par Limpide portent ces renvois ; écrits dans la question, ils font
    // dire à un passage ce que la personne veut (i03). Chaque question est indépendante : pas de passage à reprendre.
    [GeneratedRegex(@"\[p\d")]
    private static partial Regex PassageMarker();
}

using System.Text.RegularExpressions;

namespace Limpide.Core.Answering;

/// <summary>
/// Repère une réponse qui tranche la situation de la personne (« votre logiciel est à haut risque »,
/// « vous n'êtes pas en infraction ») : ce que l'assistant ne doit jamais faire (règle 5 des consignes).
/// </summary>
/// <remarks>
/// Heuristique, phrase par phrase : une phrase est signalée si elle s'adresse à la personne (vous, votre, vos)
/// <b>et</b> contient un verdict (infraction, conformité, interdit, autorisé, haut risque...). Sont exclues les
/// formulations au conditionnel (« pourrait être classé... si ») et les renvois à vérifier (« pour savoir si
/// votre système est conforme »), que les consignes autorisent. Elle signale sans bloquer : des fausses alertes
/// et des oublis restent possibles (formulations impersonnelles, par exemple). Exemples réels : Mistral Medium,
/// 2026-10-05, questions q03 et t05 (docs/notes/observations-generation.md).
/// </remarks>
public static partial class LegalQualification
{
    /// <summary>Première phrase qui tranche la situation de la personne, ou null.</summary>
    public static string? Find(string answer) =>
        Sentence().Split(answer)
            .Select(s => s.Trim())
            .FirstOrDefault(s => SecondPerson().IsMatch(s) && Verdict().IsMatch(s) && !Hedged().IsMatch(s));

    [GeneratedRegex(@"(?<=[.!?:])\s+|\n+")]
    private static partial Regex Sentence();

    [GeneratedRegex(@"\b(vous|votre|vos)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SecondPerson();

    [GeneratedRegex(
        @"\b(en infraction|en règle|en conformité|non[- ]conformes?|conformes?|illégale?s?|illicites?|légale?s?"
        + @"|interdite?s?|autorisée?s?|à haut risque|exemptée?s?|exonérée?s?|pas concernée?s?|hors du champ)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex Verdict();

    [GeneratedRegex(
        @"\b(pourrai[ets]?|pourraient|peut[- ]être|peuvent être)\b"
        + @"|\b(savoir|vérifier|déterminer|apprécier|évaluer|établir)\s+si\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex Hedged();
}

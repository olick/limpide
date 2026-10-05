namespace Limpide.Core.Evaluation;

/// <summary>Jeu de questions de test, lu depuis eval/questions.json.</summary>
public sealed record EvaluationSet(IReadOnlyList<EvaluationQuestion> Questions);

/// <param name="Id">Identifiant stable (« q01 ») : permet de comparer une question d'une mesure à l'autre.</param>
/// <param name="Expected">Passages qui répondent à la question ; requis quand on attend une réponse.</param>
/// <param name="Note">Pourquoi ce passage est attendu, ou ce que la question teste.</param>
/// <param name="Behavior">Comportement attendu de l'assistant (par défaut : répondre en citant le passage attendu).</param>
public sealed record EvaluationQuestion(
    string Id,
    string Question,
    ExpectedPassage? Expected = null,
    string? Note = null,
    ExpectedBehavior Behavior = ExpectedBehavior.Answer
);

public enum ExpectedBehavior
{
    /// <summary>Répondre, en citant au moins un des passages attendus.</summary>
    Answer,

    /// <summary>Ne pas répondre sur le fond : hors sujet, ou réponse absente du corpus.</summary>
    Decline,

    /// <summary>
    /// Demande de qualification juridique (« suis-je en infraction ? ») : l'assistant peut indiquer les textes
    /// concernés, sans trancher la situation. Vérifiable seulement à la relecture.
    /// </summary>
    NoLegalQualification,
}

/// <summary>
/// Passages considérés comme une bonne réponse : par ancre (AI Act : « art_5 ») ou, pour les sources
/// sans ancre (CNIL), par un morceau du titre de rattachement. Un seul critère satisfait suffit.
/// </summary>
public sealed record ExpectedPassage(
    IReadOnlyList<string>? Anchors = null,
    IReadOnlyList<string>? Headings = null
)
{
    public bool Matches(string? anchor, string heading) =>
        (anchor is not null && Anchors?.Contains(anchor) == true)
        || Headings?.Any(h => heading.Contains(h, StringComparison.Ordinal)) == true;

    public bool IsEmpty => (Anchors?.Count ?? 0) + (Headings?.Count ?? 0) == 0;
}

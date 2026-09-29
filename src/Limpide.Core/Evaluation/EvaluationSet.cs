namespace Limpide.Core.Evaluation;

/// <summary>Jeu de questions de test, lu depuis eval/questions.json.</summary>
public sealed record EvaluationSet(IReadOnlyList<EvaluationQuestion> Questions);

/// <param name="Id">Identifiant stable (« q01 ») : permet de comparer une question d'une mesure à l'autre.</param>
/// <param name="Note">Pourquoi ce passage est attendu, ou ce que la question teste.</param>
public sealed record EvaluationQuestion(
    string Id,
    string Question,
    ExpectedPassage Expected,
    string? Note = null
);

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
}

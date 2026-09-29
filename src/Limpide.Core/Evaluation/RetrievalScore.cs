namespace Limpide.Core.Evaluation;

/// <summary>Rang du premier bon passage pour une question (null : absent des résultats).</summary>
public sealed record QuestionOutcome(EvaluationQuestion Question, int? Rank);

/// <summary>
/// Score de la recherche seule (sans LLM) :
/// - réussite à k : part des questions dont un bon passage figure dans les k premiers résultats ;
/// - MRR (rang réciproque moyen) : 1 si le bon passage est premier, 1/2 s'il est deuxième..., 0 s'il est absent.
///   Distingue « trouvé en premier » de « trouvé en cinquième », ce que la réussite à 5 ne fait pas.
/// </summary>
public sealed record RetrievalScore(
    int Questions,
    int HitsAt1,
    int HitsAt5,
    double MeanReciprocalRank
)
{
    public static RetrievalScore From(IReadOnlyCollection<QuestionOutcome> outcomes) =>
        new(
            outcomes.Count,
            outcomes.Count(o => o.Rank == 1),
            outcomes.Count(o => o.Rank <= 5),
            outcomes.Count == 0
                ? 0
                : outcomes.Sum(o => o.Rank is { } rank ? 1.0 / rank : 0) / outcomes.Count
        );

    /// <summary>Rang du premier résultat qui correspond au passage attendu, à partir de 1.</summary>
    public static int? RankOf(
        ExpectedPassage expected,
        IEnumerable<(string? Anchor, string Heading)> results
    )
    {
        var rank = 0;
        foreach (var (anchor, heading) in results)
        {
            rank++;
            if (expected.Matches(anchor, heading))
                return rank;
        }

        return null;
    }
}

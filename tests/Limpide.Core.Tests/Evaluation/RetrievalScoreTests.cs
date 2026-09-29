using Limpide.Core.Evaluation;

namespace Limpide.Core.Tests.Evaluation;

public class RetrievalScoreTests
{
    private static readonly ExpectedPassage Article5 = new(Anchors: ["art_5"]);

    [Fact]
    public void Rank_is_the_position_of_the_first_matching_result()
    {
        (string?, string)[] results =
        [
            ("rct_29", "Considérant (29)"),
            ("art_5", "Article 5 — Pratiques interdites en matière d’IA"),
            ("art_5", "Article 5 — Pratiques interdites en matière d’IA"),
        ];

        Assert.Equal(2, RetrievalScore.RankOf(Article5, results));
    }

    [Fact]
    public void Rank_is_null_when_no_result_matches()
    {
        Assert.Null(RetrievalScore.RankOf(Article5, [("art_6", "Article 6")]));
    }

    [Fact]
    public void Passages_without_anchor_match_on_a_part_of_their_heading()
    {
        var expected = new ExpectedPassage(Headings: ["Garantir la qualité de l’annotation"]);

        Assert.True(expected.Matches(null, "IA : Annoter les données › Garantir la qualité de l’annotation"));
        Assert.False(expected.Matches(null, "IA : Annoter les données › Le principe d’exactitude"));
    }

    [Fact]
    public void Score_counts_hits_and_averages_reciprocal_ranks()
    {
        var question = new EvaluationQuestion("q", "?", Article5);
        QuestionOutcome[] outcomes = [new(question, 1), new(question, 4), new(question, 7), new(question, null)];

        var score = RetrievalScore.From(outcomes);

        Assert.Equal((4, 1, 2), (score.Questions, score.HitsAt1, score.HitsAt5));
        Assert.Equal((1 + 0.25 + 1.0 / 7) / 4, score.MeanReciprocalRank, precision: 10);
    }

    [Fact]
    public void Loader_rejects_a_question_without_expected_passage()
    {
        const string json = """{ "questions": [ { "id": "q01", "question": "?", "expected": {} } ] }""";

        var ex = Assert.Throws<InvalidDataException>(() => EvaluationSetLoader.Parse(json));
        Assert.Contains("aucun passage attendu", ex.Message);
    }

    [Fact]
    public void Repository_question_set_is_valid()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory!.FullName, "Limpide.slnx")))
            directory = directory.Parent;

        var set = EvaluationSetLoader.Load(Path.Combine(directory.FullName, "eval", "questions.json"));

        Assert.Equal(10, set.Questions.Count);
    }
}

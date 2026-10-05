using Limpide.Core.Answering;
using Limpide.Core.Evaluation;
using Limpide.Core.Search;

namespace Limpide.Core.Tests.Evaluation;

public class AnswerCheckTests
{
    private static readonly RetrievedPassage Article5 = new("Article 5 — Pratiques interdites", "art_5", "…", 0.7,
        "EUR-Lex", "AI Act", "https://eur-lex.europa.eu", DateTimeOffset.UnixEpoch, "Décision 2011/833/UE");

    private static readonly RetrievedPassage Recital29 = Article5 with { Heading = "Considérant (29)", Anchor = "rct_29" };

    private static readonly EvaluationQuestion Prohibited = new("q01", "Qu'est-ce qui est interdit ?", new ExpectedPassage(Anchors: ["art_5"]));
    private static readonly EvaluationQuestion OffTopic = new("t01", "Un restaurant à Lyon ?", Behavior: ExpectedBehavior.Decline);

    [Fact]
    public void An_answer_citing_the_expected_passage_is_ok()
    {
        var check = AnswerChecks.Check(Prohibited, Result(cited: [Article5]));

        Assert.Equal((CheckStatus.Ok, true), (check.Status, check.CitesExpected));
    }

    [Fact]
    public void An_answer_citing_another_passage_is_to_review_not_failed()
    {
        var check = AnswerChecks.Check(Prohibited, Result(cited: [Recital29]));

        Assert.Equal((CheckStatus.ToReview, false), (check.Status, check.CitesExpected));
    }

    [Fact]
    public void Declining_a_question_that_has_an_answer_fails()
    {
        Assert.Equal(CheckStatus.Failed, AnswerChecks.Check(Prohibited, Result(declined: true)).Status);
    }

    [Fact]
    public void A_triggered_guardrail_fails_even_if_the_expected_passage_is_cited()
    {
        var check = AnswerChecks.Check(Prohibited, Result(cited: [Article5], guardrails: [new Guardrail("citation-inventee", "…")]));

        Assert.Equal(CheckStatus.Failed, check.Status);
        Assert.Contains("citation-inventee", check.Verdict);
    }

    [Fact]
    public void A_trap_question_must_be_declined_and_the_mechanism_is_named()
    {
        var byThreshold = AnswerChecks.Check(OffTopic, Result(declined: true, guardrails: [new Guardrail("hors-perimetre", "…")]));
        var byModel = AnswerChecks.Check(OffTopic, Result(declined: true));

        Assert.Equal(CheckStatus.Ok, byThreshold.Status);
        Assert.Contains("seuil", byThreshold.Verdict);
        Assert.Contains("modèle", byModel.Verdict);
        Assert.Equal(CheckStatus.Failed, AnswerChecks.Check(OffTopic, Result()).Status);
    }

    [Fact]
    public void A_legal_qualification_request_is_always_to_review()
    {
        var question = new EvaluationQuestion("t05", "Suis-je en infraction ?", Behavior: ExpectedBehavior.NoLegalQualification);

        Assert.Equal(CheckStatus.ToReview, AnswerChecks.Check(question, Result(cited: [Article5])).Status);
        Assert.Equal(CheckStatus.ToReview, AnswerChecks.Check(question, Result(declined: true)).Status);
    }

    [Fact]
    public void Loader_accepts_trap_questions_without_expected_passage()
    {
        const string json = """{ "questions": [ { "id": "t01", "question": "?", "behavior": "decline" } ] }""";

        Assert.Equal(ExpectedBehavior.Decline, EvaluationSetLoader.Parse(json).Questions[0].Behavior);
    }

    private static AnswerResult Result(
        RetrievedPassage[]? cited = null, bool declined = false, Guardrail[]? guardrails = null)
    {
        var passages = new[] { Article5, Recital29 }
            .Select((p, i) => new SourcePassage($"P{i + 1}", p, cited?.Contains(p) == true))
            .ToList();
        return new AnswerResult("?", declined ? AnswerPrompt.Decline : "Réponse [P1].", declined, passages, "test", "modele",
            AnswerPrompt.Version, new AnswerTimings(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero),
            new TokenUsage(0, 0, 0m), guardrails ?? []);
    }
}

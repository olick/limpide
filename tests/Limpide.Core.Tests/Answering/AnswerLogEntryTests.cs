using Limpide.Core.Answering;
using Limpide.Core.Search;

namespace Limpide.Core.Tests.Answering;

public class AnswerLogEntryTests
{
    private static readonly RetrievedPassage Passage = new("Article 50", "art_50", "…", 0.78,
        "EUR-Lex", "AI Act", "https://eur-lex.europa.eu", DateTimeOffset.UnixEpoch, "Décision 2011/833/UE",
        new PassageOrigin(Guid.Empty, new string('a', 64), "eur-lex-xhtml/1", "ai-act/1"));

    private static AnswerResult Result(bool declined, bool cited, params Guardrail[] guardrails) => new(
        "Dois-je prévenir les utilisateurs ?", "Oui [P1].", declined,
        [new SourcePassage("P1", Passage, cited), new SourcePassage("P2", Passage with { Score = 0.65 }, Cited: false)],
        "vectorielle", "mistral-medium-2604", "answer/5",
        new AnswerTimings(TimeSpan.FromMilliseconds(80), TimeSpan.FromMilliseconds(12), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3.1)),
        new TokenUsage(2400, 300, 0.0012m), guardrails);

    [Fact]
    public void An_answer_keeps_its_measures_but_not_its_text()
    {
        var entry = AnswerLogEntry.From(Result(declined: false, cited: true, new Guardrail("reponse-tronquee", "…")), "0af7651916cd43dd8448eb211c80319c");

        Assert.Equal(AnswerOutcome.Answered, entry.Outcome);
        Assert.Equal(("mistral-medium-2604", "answer/5", 2, 1, 0.78), (entry.Model, entry.PromptVersion, entry.Passages, entry.Cited, entry.BestScore));
        Assert.Equal((2400L, 300L, 0.0012m), (entry.InputTokens!.Value, entry.OutputTokens!.Value, entry.CostEur!.Value));
        Assert.Equal(["reponse-tronquee"], entry.Guardrails);
        Assert.Equal("Dois-je prévenir les utilisateurs ?".Length, entry.QuestionLength);
        Assert.DoesNotContain(typeof(AnswerLogEntry).GetProperties(), p => p.Name is "Question" or "Answer");
    }

    [Fact]
    public void A_declined_answer_is_logged_as_i_do_not_know()
    {
        Assert.Equal(AnswerOutcome.Declined, AnswerLogEntry.From(Result(declined: true, cited: false), null).Outcome);
    }

    [Fact]
    public void Below_the_threshold_the_model_is_not_recorded_as_called()
    {
        var entry = AnswerLogEntry.From(
            Result(declined: true, cited: false, new Guardrail(AnswerService.OutOfScopeCode, "…")), null);

        Assert.Equal(AnswerOutcome.OutOfScope, entry.Outcome);
        Assert.Null(entry.Model);
    }

    [Fact]
    public void A_failure_keeps_the_exception_type_never_its_message()
    {
        var timeout = AnswerLogEntry.Failed(new OperationCanceledException("question : …"), 42, TimeSpan.FromSeconds(60), null);
        var error = AnswerLogEntry.Failed(new HttpRequestException("question : …"), 42, TimeSpan.FromSeconds(1), null);

        Assert.Equal((AnswerOutcome.Timeout, "OperationCanceledException"), (timeout.Outcome, timeout.Error));
        Assert.Equal((AnswerOutcome.Error, "HttpRequestException"), (error.Outcome, error.Error));
    }
}

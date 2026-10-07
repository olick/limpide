using Limpide.Core.Answering;
using Limpide.Core.Feedback;
using Limpide.Core.Search;

namespace Limpide.Core.Tests.Feedback;

public class VisitorFeedbackTests
{
    [Fact]
    public void An_empty_form_gives_no_feedback()
    {
        Assert.Null(VisitorFeedback.From("  ", null, "", attach: null));
    }

    [Fact]
    public void Blank_answers_are_dropped_and_text_is_trimmed()
    {
        var feedback = VisitorFeedback.From("  Un assistant sur l'AI Act. ", "   ", null, attach: null);

        Assert.Equal("Un assistant sur l'AI Act.", feedback!.Understood);
        Assert.Null(feedback.Misleading);
        Assert.Null(feedback.Context);
    }

    [Fact]
    public void Long_answers_are_cut_to_the_maximum_length()
    {
        var feedback = VisitorFeedback.From(new string('a', 5000), null, null, attach: null);

        Assert.Equal(VisitorFeedback.MaxLength, feedback!.Understood!.Length);
    }

    [Fact]
    public void The_attached_answer_keeps_what_produced_it()
    {
        var passage = new RetrievedPassage("Article 5 — Pratiques interdites", "art_5", "…", 0.7,
            "EUR-Lex", "AI Act", "https://eur-lex.europa.eu", DateTimeOffset.UnixEpoch, "Décision 2011/833/UE");
        var result = new AnswerResult("Qu'est-ce qui est interdit ?", "Ceci [P1].", false,
            [new SourcePassage("P1", passage, Cited: true)], "vectorielle", "mistral-medium-2604", "answer/3",
            new AnswerTimings(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero), new TokenUsage(1, 1, 0m), []);

        var context = VisitorFeedback.From(null, "La réponse oublie un cas.", null, attach: result)!.Context!;

        Assert.Equal(("Qu'est-ce qui est interdit ?", "mistral-medium-2604", "answer/3"), (context.Question, context.Model, context.PromptVersion));
        Assert.Equal(["Article 5 — Pratiques interdites (art_5)"], context.Cited);
    }
}

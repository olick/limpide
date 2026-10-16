using Limpide.Core.Evaluation;

namespace Limpide.Core.Tests.Evaluation;

public class FaithfulnessJudgeTests
{
    [Fact]
    public void Reads_the_verdict_and_the_issues()
    {
        var (verdict, issues) = FaithfulnessJudge.Parse("""
            {"problemes":[{"extrait":"Oui, votre logiciel est à haut risque","type":"qualification","explication":"tranche le cas"}],"verdict":"infidele"}
            """);

        Assert.Equal(FaithfulnessVerdict.Unfaithful, verdict);
        Assert.Equal("qualification", Assert.Single(issues).Kind);
    }

    [Fact]
    public void A_missing_or_unknown_verdict_is_unreadable_never_faithful()
    {
        Assert.Equal(FaithfulnessVerdict.Unreadable, FaithfulnessJudge.Parse("La réponse est fidèle.").Verdict);
        Assert.Equal(FaithfulnessVerdict.Unreadable, FaithfulnessJudge.Parse("""{"problemes":[]}""").Verdict);
        Assert.Equal(FaithfulnessVerdict.Unreadable, FaithfulnessJudge.Parse("""{"verdict":"plutôt bien"}""").Verdict);
    }

    [Fact]
    public void A_serious_issue_overrides_a_lenient_verdict()
    {
        var json = """{"problemes":[{"extrait":"…","type":"deforme","explication":"…"}],"verdict":"fidele"}""";

        Assert.Equal(FaithfulnessVerdict.Unfaithful, FaithfulnessJudge.Parse(json).Verdict);
    }

    [Fact]
    public void Minor_issues_keep_a_reservation()
    {
        var json = """{"problemes":[{"extrait":"…","type":"mineur","explication":"…"}],"verdict":"reserve"}""";

        Assert.Equal(FaithfulnessVerdict.Reservation, FaithfulnessJudge.Parse(json).Verdict);
    }

    [Fact]
    public void Accepts_a_json_code_block()
    {
        Assert.Equal(FaithfulnessVerdict.Faithful, FaithfulnessJudge.Parse("```json\n{\"problemes\":[],\"verdict\":\"fidele\"}\n```").Verdict);
    }
}

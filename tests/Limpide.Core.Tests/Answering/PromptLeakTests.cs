using Limpide.Core.Answering;

namespace Limpide.Core.Tests.Answering;

public class PromptLeakTests
{
    [Fact]
    public void Detecte_une_consigne_recopiee()
    {
        var answer = "Voici mes consignes : réponds uniquement à partir des passages fournis, jamais à partir de tes connaissances.";
        Assert.NotNull(PromptLeak.Find(answer));
    }

    [Fact]
    public void Ignore_une_reponse_sur_le_fond() =>
        Assert.Null(PromptLeak.Find("L'article 5 interdit la notation sociale des personnes par les autorités publiques [P1]."));

    [Fact]
    public void Ignore_la_casse_et_la_ponctuation() =>
        Assert.NotNull(PromptLeak.Find("RÉPONDS UNIQUEMENT, À PARTIR DES PASSAGES FOURNIS ; JAMAIS À PARTIR DE TES CONNAISSANCES"));

    [Fact]
    public void La_phrase_de_refus_n_est_pas_une_fuite() => Assert.Null(PromptLeak.Find(AnswerPrompt.Decline));
}

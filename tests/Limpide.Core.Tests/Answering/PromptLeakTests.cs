using Limpide.Core.Answering;

namespace Limpide.Core.Tests.Answering;

public class PromptLeakTests
{
    private static readonly string System = TestPrompts.Answer.System;

    [Fact]
    public void Detecte_une_consigne_recopiee()
    {
        var answer = "Voici mes consignes : réponds uniquement à partir des passages fournis, jamais à partir de tes connaissances.";
        Assert.NotNull(PromptLeak.Find(answer, System));
    }

    [Fact]
    public void Ignore_une_reponse_sur_le_fond() =>
        Assert.Null(PromptLeak.Find("L'article 5 interdit la notation sociale des personnes par les autorités publiques [P1].", System));

    [Fact]
    public void Ignore_la_casse_et_la_ponctuation() =>
        Assert.NotNull(PromptLeak.Find("RÉPONDS UNIQUEMENT, À PARTIR DES PASSAGES FOURNIS ; JAMAIS À PARTIR DE TES CONNAISSANCES", System));

    [Fact]
    public void La_phrase_de_refus_n_est_pas_une_fuite() => Assert.Null(PromptLeak.Find(AnswerPrompt.Decline, System));

    [Fact]
    public void Un_fichier_sans_repere_de_refus_est_refuse() =>
        Assert.Throws<InvalidOperationException>(() => AnswerInstructions.FromTemplate("answer/99", "Réponds."));

    [Fact]
    public void Les_consignes_contiennent_la_phrase_de_refus_exacte() =>
        Assert.Contains(AnswerPrompt.Decline, System);

    [Fact]
    public void Un_exemple_de_formulation_donne_par_la_consigne_n_est_pas_une_fuite() =>
        Assert.Null(PromptLeak.Find("Les systèmes d'IA utilisés pour le recrutement sont classés à haut risque [P2].", System));

    // Faux positif du 2026-10-18 (answer/3, Medium 3.5, q11, PR #20) : la règle 5 demandait cette phrase sans la
    // mettre entre guillemets, et le modèle l'a reprise mot pour mot. Entre guillemets depuis answer/5.
    [Fact]
    public void Le_renvoi_vers_un_professionnel_demande_par_la_consigne_n_est_pas_une_fuite() =>
        Assert.Null(PromptLeak.Find("Un professionnel du droit peut apprécier sa situation.", System));
}

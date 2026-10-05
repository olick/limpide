using Limpide.Core.Answering;

namespace Limpide.Core.Tests.Answering;

public class LegalQualificationTests
{
    [Theory] // Phrases réelles de Mistral Medium (2026-10-05), qui tranchent la situation de la personne.
    [InlineData("Oui, votre logiciel qui trie automatiquement les CV des candidats est considéré comme un système d'IA à haut risque, car il est utilisé pour des questions liées à l'emploi [P3].")]
    [InlineData("Si votre logiciel ne relève pas de ces cas, il n'est pas interdit, mais il est **à haut risque** et doit se conformer aux exigences de l'AI Act [P2][P5].")]
    [InlineData("En revanche, aucune disposition ne les interdit explicitement dans ce contexte. Vous n'êtes donc pas *en infraction* par principe.")]
    public void Flags_answers_that_decide_the_persons_situation(string answer)
    {
        Assert.NotNull(LegalQualification.Find(answer));
    }

    [Theory] // Formulations permises : la règle, ses conditions, un renvoi vers un professionnel.
    [InlineData("Votre logiciel pourrait être classé comme un système à haut risque si son utilisation a un impact significatif sur les candidats [P3].")]
    [InlineData("Les systèmes d'IA utilisés pour le recrutement sont classés à haut risque [P2].")]
    [InlineData("Oui, vous devez prévenir les utilisateurs qu'ils interagissent avec un système d'IA, sauf si cela ressort clairement du contexte [P1].")]
    [InlineData("Pour savoir si votre système est conforme, un professionnel du droit peut apprécier votre situation.")]
    [InlineData("Le règlement n'interdit pas explicitement cette pratique, mais impose des obligations strictes [P5].")]
    public void Lets_through_rules_conditions_and_referrals(string answer)
    {
        Assert.Null(LegalQualification.Find(answer));
    }

    [Fact]
    public void Returns_the_offending_sentence_only()
    {
        var found = LegalQualification.Find("Le recrutement est à haut risque [P1]. Votre outil est donc interdit. Consultez un avocat.");

        Assert.Equal("Votre outil est donc interdit.", found);
    }
}

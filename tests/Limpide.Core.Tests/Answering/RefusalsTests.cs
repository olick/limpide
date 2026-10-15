using Limpide.Core.Answering;

namespace Limpide.Core.Tests.Answering;

public class RefusalsTests
{
    [Theory]
    [InlineData("Je ne peux pas répondre à cette demande.")]
    [InlineData("« Je ne sais pas : les textes consultés ne permettent pas de répondre. »")]
    [InlineData("Je ne suis pas en mesure de répondre à cette question.")]
    public void Reconnait_un_refus_court_sans_citation(string answer) => Assert.True(Refusals.IsRefusal(answer, 0));

    [Fact]
    public void Une_reponse_citee_n_est_pas_un_refus() =>
        Assert.False(Refusals.IsRefusal("Je ne peux pas trancher votre cas, mais l'article 5 interdit la notation sociale [P1].", 1));

    [Fact]
    public void Une_reponse_longue_sur_le_fond_n_est_pas_un_refus() =>
        Assert.False(Refusals.IsRefusal("Je ne peux pas être exhaustif : " + new string('x', 300), 0));
}

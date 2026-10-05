using Limpide.Core.Answering;

namespace Limpide.Core.Tests.Answering;

public class CitationsTests
{
    [Fact]
    public void Finds_cited_passages_in_order_of_first_citation()
    {
        var check = Citations.Check("Interdit [P3]. Sauf exception [P1][P3].", passageCount: 5);

        Assert.Equal([3, 1], check.Cited);
        Assert.Empty(check.Invented);
    }

    [Theory]
    [InlineData("[P1, P2]")]
    [InlineData("[P1, 2]")]
    [InlineData("[p1; p2]")]
    [InlineData("[P1a][P2e]")] // lettre du point de l'article, ajoutée par Mistral Small et Medium
    public void Accepts_the_variants_models_produce(string text)
    {
        Assert.Equal([1, 2], Citations.Check(text, passageCount: 5).Cited);
    }

    [Fact]
    public void Reports_identifiers_outside_the_passages_provided()
    {
        var check = Citations.Check("Voir [P2] et [P7].", passageCount: 5);

        Assert.Equal([2], check.Cited);
        Assert.Equal(["P7"], check.Invented);
    }

    [Theory]
    [InlineData("Selon l'article 5 [1], c'est interdit.")] // pas un identifiant de passage
    [InlineData("Aucune citation.")]
    public void Ignores_text_that_is_not_a_passage_identifier(string text)
    {
        var check = Citations.Check(text, passageCount: 5);

        Assert.Empty(check.Cited);
        Assert.Empty(check.Invented);
    }
}

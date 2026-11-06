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
    [InlineData("[P1.1.a][P2.c]")] // paragraphe et point, ajoutés par Mistral Medium
    [InlineData("[P1, point a][P2]")]
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

    private static string Linked(string text) => Citations.Link(text, (id, shown) => id == "P9" ? null : $"<{id}>{shown}</{id}>");

    [Theory] // Formes réelles de Mistral Medium (rapports du 2026-11-06).
    [InlineData("[P1]", "<P1>[P1]</P1>")]
    [InlineData("[P1a]", "<P1>[P1a]</P1>")]
    [InlineData("[P6.3]", "<P6>[P6.3]</P6>")]
    [InlineData("[P4.6 bis]", "<P4>[P4.6 bis]</P4>")]
    [InlineData("[P1c.ii]", "<P1>[P1c.ii]</P1>")]
    [InlineData("[P1, paragraphe 4]", "<P1>[P1, paragraphe 4]</P1>")]
    [InlineData("[P2, §3]", "<P2>[P2, §3]</P2>")]
    public void A_detailed_reference_links_to_its_passage(string citation, string expected)
    {
        Assert.Equal(expected, Linked(citation));
    }

    [Fact]
    public void Several_passages_in_one_bracket_get_one_link_each()
    {
        Assert.Equal("[<P1>P1</P1>, <P3>P3</P3>]", Linked("[P1, P3]"));
    }

    [Theory]
    [InlineData("[P1; P3]", "[<P1>P1</P1>; <P3>P3</P3>]")] // séparateur d'origine conservé
    [InlineData("[P01]", "<P1>[P01]</P1>")]
    [InlineData("[P1 &amp; suite]", "<P1>[P1 &amp; suite]</P1>")] // entité HTML intacte
    [InlineData("[P1 <em>x</em>]", "[P1 <em>x</em>]")] // une balise dans les crochets : pas de lien
    public void Html_and_separators_survive_linking(string citation, string expected)
    {
        Assert.Equal(expected, Linked(citation));
    }

    [Fact]
    public void A_citation_without_link_stays_as_it_is_and_other_text_is_untouched()
    {
        Assert.Equal("Sauf [P9.2] et [note] [P2].".Replace("[P2]", "<P2>[P2]</P2>"), Linked("Sauf [P9.2] et [note] [P2]."));
    }

    [Theory] // Ce que le contrôle compte comme une citation s'affiche comme une citation.
    [InlineData("[P6.3]")]
    [InlineData("[p2]")]
    [InlineData("[P1, 3]")]
    public void Linking_reads_citations_like_the_check(string citation)
    {
        var linked = new List<string>();
        Citations.Link(citation, (id, _) => { linked.Add(id); return null; });

        Assert.Equal(Citations.Check(citation, passageCount: 6).Cited.Select(n => $"P{n}"), linked);
    }
}

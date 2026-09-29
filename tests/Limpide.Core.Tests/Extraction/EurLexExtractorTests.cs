using Limpide.Core.Extraction;

namespace Limpide.Core.Tests.Extraction;

public class EurLexExtractorTests
{
    private static readonly IReadOnlyList<Block> Blocks = Fixtures.Extract(new EurLexExtractor(), "ai-act-extraits.xhtml");

    [Fact]
    public void Chapters_sections_and_articles_become_headings_with_their_title()
    {
        var headings = Blocks.Where(b => b.Kind == BlockKind.Heading).ToList();

        Assert.Contains(new Block(BlockKind.Heading, "CHAPITRE I — DISPOSITIONS GÉNÉRALES", 1, "cpt_I"), headings);
        Assert.Contains(new Block(BlockKind.Heading, "SECTION 1 — Classification de systèmes d’IA comme systèmes à haut risque", 2, "cpt_III.sct_1"), headings);
        Assert.Contains(new Block(BlockKind.Heading, "Article 5 — Pratiques interdites en matière d’IA", 3, "art_5"), headings);
        Assert.Contains(new Block(BlockKind.Heading, "ANNEXE I — Liste de la législation d'harmonisation de l'Union", 1, "anx_I"), headings);
    }

    [Fact]
    public void Numbered_paragraphs_keep_their_eli_anchor()
    {
        var paragraph = Blocks.Single(b => b.Text.StartsWith("1. Les pratiques en matière d’IA suivantes sont interdites"));

        Assert.Equal(BlockKind.Paragraph, paragraph.Kind);
        Assert.Equal("005.001", paragraph.Anchor);
    }

    [Fact]
    public void Enumerations_become_list_items_with_their_label_and_depth()
    {
        var pointC = Blocks.Single(b => b.Text.StartsWith("c) la mise sur le marché, la mise en service ou l’utilisation de systèmes d’IA pour l’évaluation"));
        var index = Blocks.ToList().IndexOf(pointC);
        var subPoint = Blocks[index + 1];

        Assert.Equal((BlockKind.ListItem, 1, "005.001"), (pointC.Kind, pointC.Level, pointC.Anchor));
        Assert.StartsWith("i) le traitement préjudiciable", subPoint.Text);
        Assert.Equal(2, subPoint.Level);
    }

    [Fact]
    public void Recitals_are_numbered_items_anchored_to_the_recital()
    {
        var recital = Blocks.Single(b => b.Anchor == "rct_1");

        Assert.StartsWith("(1) L’objectif du présent règlement est d’améliorer le fonctionnement du marché intérieur", recital.Text);
    }

    [Fact]
    public void Footnote_calls_and_notes_are_removed_without_leaving_a_space_before_punctuation()
    {
        var citation = Blocks.Single(b => b.Anchor == "cit_4");

        Assert.Equal("vu l’avis du Comité économique et social européen,", citation.Text);
        Assert.DoesNotContain(Blocks, b => b.Text.Contains("JO C 517"));
    }

    [Theory]
    [InlineData("RÈGLEMENT (UE) 2024/1689 DU PARLEMENT")] // titre, déjà en base
    [InlineData("Fait à Bruxelles")]                      // formule finale
    [InlineData("METSOLA")]                               // signatures
    [InlineData("Journal officiel")]                      // en-tête du JO
    [InlineData("ISSN")]                                  // pied du JO
    public void Page_furniture_is_not_extracted(string text)
    {
        Assert.DoesNotContain(Blocks, b => b.Text.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Whitespace_is_normalized_but_text_is_otherwise_untouched()
    {
        // Source : « 1.&#160;&#160;&#160;L’objectif » sur plusieurs lignes d'indentation.
        Assert.All(Blocks, b => Assert.DoesNotMatch(@"\s{2}|^\s|\s$| ", b.Text));
        Assert.Contains(Blocks, b => b.Text.StartsWith("1. L’objectif du présent règlement est d’améliorer"));
    }

    [Fact]
    public void Rejects_a_page_that_is_not_a_cellar_xhtml_act()
    {
        Assert.Throws<InvalidDataException>(() => Fixtures.Extract(new EurLexExtractor(), "cnil-annoter-les-donnees.html"));
    }
}

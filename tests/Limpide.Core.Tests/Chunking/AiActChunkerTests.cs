using Limpide.Core.Chunking;
using Limpide.Core.Extraction;
using Limpide.Core.Tests.Extraction;

namespace Limpide.Core.Tests.Chunking;

public class AiActChunkerTests
{
    private static readonly IReadOnlyList<Block> Blocks = Fixtures.Extract(new EurLexExtractor(), "ai-act-extraits.xhtml");
    private static readonly IReadOnlyList<Chunk> Chunks = new AiActChunker().Split(Blocks);

    [Fact]
    public void A_short_article_is_a_single_passage_titled_with_the_article()
    {
        var article1 = Chunks.Where(c => c.Anchor == "art_1").ToList();

        var chunk = Assert.Single(article1);
        Assert.Equal("Article premier — Objet", chunk.Heading);
        Assert.StartsWith("1. L’objectif du présent règlement", chunk.Content);
    }

    [Fact]
    public void A_long_article_is_split_between_its_numbered_paragraphs_without_mixing_articles()
    {
        var article5 = Chunks.Where(c => c.Anchor == "art_5").ToList();

        Assert.True(article5.Count > 1);
        Assert.All(article5, c => Assert.Equal("Article 5 — Pratiques interdites en matière d’IA", c.Heading));
        Assert.StartsWith("1. Les pratiques en matière d’IA suivantes sont interdites:", article5[0].Content);
        Assert.Contains(article5, c => c.Content.StartsWith("2. "));
        Assert.All(article5, c => Assert.True(c.Content.Length <= Packer.MaxLength + Packer.MinLength, c.Content[..60]));
    }

    [Fact]
    public void Recitals_and_annexes_are_units_of_their_own()
    {
        Assert.Equal("Considérant (1)", Assert.Single(Chunks, c => c.Anchor == "rct_1").Heading);
        Assert.All(Chunks.Where(c => c.Anchor == "anx_I"),
            c => Assert.Equal("ANNEXE I — Liste de la législation d'harmonisation de l'Union", c.Heading));
    }

    [Fact]
    public void Chapter_and_section_titles_only_delimit_units()
    {
        Assert.DoesNotContain(Chunks, c => c.Content.Contains("DISPOSITIONS GÉNÉRALES"));
        Assert.DoesNotContain(Chunks, c => c.Anchor is not null && c.Anchor.StartsWith("cpt_"));
    }

    [Fact]
    public void Every_block_of_text_is_reproduced_word_for_word_in_a_passage()
    {
        var textBlocks = Blocks.Where(b => b.Kind != BlockKind.Heading || b.Anchor!.StartsWith("anx_") && b.Level == 2);
        var all = string.Join("\n", Chunks.Select(c => c.Content));

        Assert.All(textBlocks, b => Assert.Contains(b.Text, all));
    }

    [Fact]
    public void The_adoption_formula_is_dropped()
    {
        Block[] blocks =
        [
            new(BlockKind.Paragraph, "LE PARLEMENT EUROPÉEN ET LE CONSEIL DE L’UNION EUROPÉENNE,", 0, "pbl_1"),
            new(BlockKind.ListItem, "(1) L’objectif du présent règlement.", 1, "rct_1"),
            new(BlockKind.Paragraph, "ONT ADOPTÉ LE PRÉSENT RÈGLEMENT:", 0, "pbl_1"),
            new(BlockKind.Heading, "Article premier — Objet", 3, "art_1"),
            new(BlockKind.Paragraph, "1. L’objectif.", 0, "001.001"),
        ];

        var chunks = new AiActChunker().Split(blocks);

        Assert.Equal(["pbl_1", "rct_1", "art_1"], chunks.Select(c => c.Anchor));
        Assert.DoesNotContain(chunks, c => c.Content.Contains("ONT ADOPTÉ"));
    }

    [Fact]
    public void Text_outside_any_unit_is_an_error_rather_than_a_silent_loss()
    {
        Block[] blocks = [new(BlockKind.Paragraph, "Texte orphelin", 0, "000.001")];

        Assert.Throws<InvalidDataException>(() => new AiActChunker().Split(blocks));
    }
}

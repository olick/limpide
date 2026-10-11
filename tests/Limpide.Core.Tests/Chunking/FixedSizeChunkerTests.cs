using Limpide.Core.Chunking;
using Limpide.Core.Extraction;

namespace Limpide.Core.Tests.Chunking;

public class FixedSizeChunkerTests
{
    private static readonly string Long = string.Join(' ', Enumerable.Repeat("mot", 400)); // 1 599 caractères

    [Fact]
    public void Cuts_around_the_size_without_splitting_a_word_and_with_overlap()
    {
        var chunks = new FixedSizeChunker("cnil", size: 1_000, overlap: 200).Split([new Block(BlockKind.Paragraph, Long)]);

        Assert.Equal(2, chunks.Count);
        Assert.All(chunks, c => Assert.True(c.Content.Length <= 1_000));
        Assert.All(chunks, c => Assert.DoesNotContain(c.Content.Split(' '), w => w != "mot"));
        // Le second passage reprend environ 200 caractères du premier.
        Assert.True(chunks[0].Content.Length + chunks[1].Content.Length > Long.Length + 150);
    }

    [Fact]
    public void Ignores_article_boundaries_but_keeps_the_context_where_a_passage_starts()
    {
        Block[] blocks =
        [
            new(BlockKind.Heading, "Article 26 — Obligations des déployeurs", 3, "art_26"),
            new(BlockKind.Paragraph, Long, 0, "026.001"),
            new(BlockKind.Heading, "Article 27 — Analyse d'impact", 3, "art_27"),
            new(BlockKind.Paragraph, "Avant de déployer un système d'IA à haut risque…", 0, "027.001"),
        ];

        var chunks = new FixedSizeChunker("eur-lex").Split(blocks);

        Assert.Equal(("Article 26 — Obligations des déployeurs", "art_26"), (chunks[0].Heading, chunks[0].Anchor));
        // Le dernier passage chevauche la fin de l'article 26 et le début de l'article 27 : il est rattaché au 26.
        Assert.Contains("Article 27", chunks[^1].Content);
        Assert.Equal("art_26", chunks[^1].Anchor);
    }

    [Fact]
    public void A_recital_is_titled_like_the_structural_chunker_does()
    {
        var chunks = new FixedSizeChunker("eur-lex").Split([new Block(BlockKind.Paragraph, "(12) La notion de système d'IA…", 0, "rct_12")]);
        Assert.Equal(("Considérant (12)", "rct_12"), (chunks[0].Heading, chunks[0].Anchor));
    }
}

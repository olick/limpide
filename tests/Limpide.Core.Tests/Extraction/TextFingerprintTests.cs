using Limpide.Core.Extraction;

namespace Limpide.Core.Tests.Extraction;

public class TextFingerprintTests
{
    private static readonly Block[] Text =
    [
        new(BlockKind.Heading, "Article 5", 2, "art_5"),
        new(BlockKind.Paragraph, "Les pratiques suivantes sont interdites :"),
        new(BlockKind.ListItem, "a) la manipulation ;", 1),
    ];

    [Fact]
    public void Same_text_gives_the_same_fingerprint()
    {
        Assert.Equal(TextFingerprint.Compute(Text), TextFingerprint.Compute(Text.ToArray()));
        Assert.Matches("^[0-9a-f]{64}$", TextFingerprint.Compute(Text));
    }

    [Fact]
    public void A_changed_word_changes_the_fingerprint()
    {
        var changed = Text.ToArray();
        changed[2] = changed[2] with { Text = "a) la manipulation subliminale ;" };
        Assert.NotEqual(TextFingerprint.Compute(Text), TextFingerprint.Compute(changed));
    }

    [Fact]
    public void Structure_counts_not_only_the_words()
    {
        var asParagraph = Text.ToArray();
        asParagraph[0] = asParagraph[0] with { Kind = BlockKind.Paragraph, Level = 0 };
        Assert.NotEqual(TextFingerprint.Compute(Text), TextFingerprint.Compute(asParagraph));
    }

    [Fact]
    public void Blocks_cannot_be_merged_into_the_same_fingerprint()
    {
        Block[] two = [new(BlockKind.Paragraph, "ab"), new(BlockKind.Paragraph, "c")];
        Block[] other = [new(BlockKind.Paragraph, "a"), new(BlockKind.Paragraph, "bc")];
        Assert.NotEqual(TextFingerprint.Compute(two), TextFingerprint.Compute(other));
    }
}

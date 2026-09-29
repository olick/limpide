using Limpide.Core.Chunking;

namespace Limpide.Core.Tests.Chunking;

public class PackerTests
{
    private static string Sentence(int length, char letter = 'a') => "Une " + new string(letter, length - 5) + ".";

    [Fact]
    public void A_short_unit_gives_a_single_passage_with_one_line_per_block()
    {
        var chunks = Packer.Pack([new UnitBlock("1. Premier alinéa.", "1"), new UnitBlock("a) premier point;", "1")]);

        Assert.Equal(["1. Premier alinéa.\na) premier point;"], chunks);
    }

    [Fact]
    public void A_long_unit_is_split_between_groups_once_the_minimum_is_reached()
    {
        var chunks = Packer.Pack(
        [
            new UnitBlock(Sentence(600), "005.001"),
            new UnitBlock(Sentence(300), "005.001"),
            new UnitBlock(Sentence(700), "005.002"),
        ]);

        Assert.Equal(2, chunks.Count);
        Assert.Equal(901, chunks[0].Length); // paragraphe 1 entier, la coupe tombe sur le changement de groupe
    }

    [Fact]
    public void A_block_longer_than_the_maximum_is_split_between_sentences_and_nothing_is_lost()
    {
        var sentences = Enumerable.Range(0, 8).Select(i => Sentence(400, (char)('a' + i))).ToList();
        var block = string.Join(" ", sentences);

        var chunks = Packer.Pack([new UnitBlock(block, null)]);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.InRange(c.Length, Packer.MinLength, Packer.MaxLength));
        Assert.All(chunks, c => Assert.EndsWith(".", c));
        Assert.Equal(block, string.Join(" ", chunks));
    }

    [Fact]
    public void A_sentence_longer_than_the_maximum_is_never_cut()
    {
        var sentence = Sentence(2500);

        Assert.Equal([sentence], Packer.Pack([new UnitBlock(sentence, null)]));
    }

    [Fact]
    public void A_short_remainder_joins_the_previous_passage()
    {
        var chunks = Packer.Pack(
        [
            new UnitBlock(Sentence(1400), "1"),
            new UnitBlock(Sentence(200), "2"),
        ]);

        Assert.Single(chunks);
    }

    [Fact]
    public void Splitting_a_long_block_keeps_the_text_intact_whatever_the_punctuation()
    {
        var text = string.Join(" ", Enumerable.Repeat(
            "Voir l’art. 5 du règlement (UE) 2024/1689. Les données; les personnes; « Le droit » s’applique! Suite", 40));

        var chunks = Packer.Pack([new UnitBlock(text, null)]);

        Assert.True(chunks.Count > 1);
        Assert.Equal(text, string.Join(" ", chunks));
        Assert.DoesNotContain(chunks, c => c.StartsWith("5 du règlement")); // « art. 5 » n'est pas une fin de phrase
    }
}

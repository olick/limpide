using Limpide.Core.Chunking;
using Limpide.Core.Extraction;
using Limpide.Core.Tests.Extraction;

namespace Limpide.Core.Tests.Chunking;

public class CnilChunkerTests
{
    private static readonly IReadOnlyList<Block> Blocks = Fixtures.Extract(new CnilExtractor(), "cnil-annoter-les-donnees.html");
    private static readonly IReadOnlyList<Chunk> Chunks = new CnilChunker().Split(Blocks);

    [Fact]
    public void Heading_is_the_path_of_section_titles()
    {
        Assert.Contains(Chunks, c => c.Heading ==
            "IA : Annoter les données › Les enjeux de l’annotation pour les droits et libertés des personnes › Le principe de minimisation");
        Assert.Equal("IA : Annoter les données", Chunks[0].Heading); // introduction, avant la première section
    }

    [Fact]
    public void A_new_section_replaces_the_previous_one_at_the_same_level()
    {
        Assert.Contains(Chunks, c => c.Heading == "IA : Annoter les données › Garantir la qualité de l’annotation");
        Assert.DoesNotContain(Chunks, c => c.Heading.Contains("minimisation › Garantir"));
    }

    [Fact]
    public void The_whole_text_is_reproduced_in_order_without_modification()
    {
        // Un bloc trop long peut être coupé entre deux phrases, sur deux passages : on compare donc
        // le texte sans tenir compte de la nature des séparateurs (espace ou retour à la ligne).
        static string Words(IEnumerable<string> texts) => string.Join(" ", texts.SelectMany(t => t.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));

        Assert.Equal(
            Words(Blocks.Where(b => b.Kind != BlockKind.Heading).Select(b => b.Text)),
            Words(Chunks.Select(c => c.Content)));
    }

    [Fact]
    public void Passages_stay_within_the_size_bounds()
    {
        Assert.All(Chunks, c => Assert.InRange(c.Content.Length, 1, Packer.MaxLength + Packer.MinLength));
        Assert.All(Chunks, c => Assert.Null(c.Anchor));
    }

    [Fact]
    public void A_list_stays_with_the_sentence_that_introduces_it_when_it_fits()
    {
        var intro = Assert.Single(Chunks, c => c.Content.Contains("Exemples d’annotations :"));

        Assert.Contains("Afin d’entraîner un modèle d’IA de reconnaissance du locuteur", intro.Content);
    }
}

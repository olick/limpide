using Limpide.Core.Extraction;

namespace Limpide.Core.Tests.Extraction;

public class CnilExtractorTests
{
    private static readonly IReadOnlyList<Block> Blocks = Fixtures.Extract(new CnilExtractor(), "cnil-annoter-les-donnees.html");

    [Fact]
    public void Starts_with_the_page_title_then_the_introduction()
    {
        Assert.Equal(new Block(BlockKind.Heading, "IA : Annoter les données", 1), Blocks[0]);
        Assert.StartsWith("La phase d’annotation des données est cruciale", Blocks[1].Text);
    }

    [Fact]
    public void Collapsible_sections_become_headings_without_their_toggle_button()
    {
        Assert.Contains(new Block(BlockKind.Heading, "Les enjeux de l’annotation pour les droits et libertés des personnes", 2), Blocks);
        Assert.Contains(new Block(BlockKind.Heading, "Le principe de minimisation", 3), Blocks);
    }

    [Fact]
    public void Nested_lists_keep_their_depth()
    {
        Assert.Contains(new Block(BlockKind.ListItem, "être documentée ;", 3), Blocks);
    }

    [Fact]
    public void Glossary_terms_stay_in_the_sentence()
    {
        // « régurgitation » est un bouton d'infobulle dans la page : son texte doit rester.
        Assert.Contains(Blocks, b => b.Text.Contains("S’interroger sur le risque de régurgitation et d’inférence"));
    }

    [Theory]
    [InlineData("Déplier")]                         // bouton des sections repliables
    [InlineData("Fiche précédente")]                // navigation entre fiches
    [InlineData("Sommaire")]
    [InlineData("Ceci peut également vous intéresser")]
    [InlineData("cookies")]                         // bandeau et pied de page
    [InlineData("Rechercher")]                      // formulaire de recherche
    [InlineData("Imprimer")]                        // outils de partage
    [InlineData("#Intelligence artificielle")]      // mots-clés
    [InlineData("22 juillet 2025")]                 // date de publication
    public void Page_furniture_is_not_extracted(string text)
    {
        Assert.DoesNotContain(Blocks, b => b.Text.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rejects_a_page_without_the_cnil_content_block()
    {
        Assert.Throws<InvalidDataException>(() => Fixtures.Extract(new CnilExtractor(), "ai-act-extraits.xhtml"));
    }
}

using System.Text;
using Limpide.Core.Extraction;

namespace Limpide.Core.Tests.Extraction;

/// <summary>Extraits de la version consolidée de l'AI Act au 27.7.2026 (CELEX 02024R1689-20260727, Omnibus 2026/1744).</summary>
public class EurLexConsolidatedExtractorTests
{
    private const string Fixture = "ai-act-consolide-extraits.html";
    private static readonly IReadOnlyList<Block> Blocks = Fixtures.Extract(new EurLexConsolidatedExtractor(), Fixture);

    [Fact]
    public void Chapters_sections_articles_and_annexes_become_headings_with_their_title()
    {
        var headings = Blocks.Where(b => b.Kind == BlockKind.Heading).ToList();

        Assert.Contains(new Block(BlockKind.Heading, "CHAPITRE I — DISPOSITIONS GÉNÉRALES", 1, "cpt_I"), headings);
        Assert.Contains(new Block(BlockKind.Heading, "Article 4 — Maîtrise de l'IA", 3, "art_4"), headings);
        Assert.Contains(new Block(BlockKind.Heading, "ANNEXE I — Liste de la législation d'harmonisation de l'Union", 1, "anx_I"), headings);
    }

    [Fact]
    public void Inserted_article_and_annex_keep_their_eli_identifier_and_title()
    {
        var article = Assert.Single(Blocks, b => b.Kind == BlockKind.Heading && b.Anchor == "art_4a");
        var annex = Assert.Single(Blocks, b => b.Kind == BlockKind.Heading && b.Level == 1 && b.Anchor == "anx_XIV");

        Assert.StartsWith("Article 4 bis — Traitement de catégories particulières de données", article.Text);
        // Intitulé de l'annexe XIV dans un p.norm, pas dans un p.title-annex-2.
        Assert.StartsWith("ANNEXE XIV — Liste des codes, des catégories", annex.Text);
    }

    [Fact]
    public void Amended_text_is_the_one_in_force()
    {
        Assert.Contains(Blocks, b => b.Text.StartsWith("1. Les fournisseurs et les déployeurs de systèmes d'IA prennent des mesures pour favoriser"));
        Assert.Contains(Blocks, b => b.Anchor == "art_113" && b.Text.StartsWith("i) du 2 décembre 2027"));
    }

    [Fact]
    public void Numbered_paragraphs_get_an_anchor_on_the_official_journal_scheme()
    {
        Assert.Equal("004.001", Blocks.Single(b => b.Text.StartsWith("1. Les fournisseurs et les déployeurs")).Anchor);
        Assert.Equal("004a.002", Blocks.Single(b => b.Text.StartsWith("2. Les fournisseurs et les déployeurs d'autres systèmes")).Anchor);
    }

    [Fact]
    public void Following_subparagraphs_stay_with_their_numbered_paragraph()
    {
        // Article 15, paragraphe 4 : deux alinéas sans numéro, frères du paragraphe dans ce format.
        var subparagraph = Blocks.Single(b => b.Text.StartsWith("Des solutions techniques redondantes"));

        Assert.Equal("015.004", subparagraph.Anchor);
    }

    [Fact]
    public void Enumerations_become_list_items_with_their_label_and_depth()
    {
        var pointC = Blocks.Single(b => b.Text.StartsWith("c) le chapitre III, sections 1, 2 et 3"));
        var subPoint = Blocks[Blocks.ToList().IndexOf(pointC) + 1];

        Assert.Equal((BlockKind.ListItem, 1, "art_113"), (pointC.Kind, pointC.Level, pointC.Anchor));
        Assert.Equal((BlockKind.ListItem, 2), (subPoint.Kind, subPoint.Level));
        Assert.StartsWith("i) du 2 décembre 2027", subPoint.Text);
    }

    [Fact]
    public void Inline_elements_do_not_split_or_space_the_sentence()
    {
        Assert.Contains(Blocks, b => b.Text == "Le présent règlement entre en vigueur le vingtième jour suivant celui de sa publication au Journal officiel de l’Union européenne.");
        Assert.Contains(Blocks, b => b.Text.Contains("règlement (UE) no 1025/2012"));
    }

    [Fact]
    public void Footnote_calls_and_notes_are_removed_with_their_parentheses()
    {
        Assert.Contains(Blocks, b => b.Text.Contains("du Parlement européen et du Conseil et sans retard injustifié"));
        Assert.DoesNotContain(Blocks, b => b.Text.Contains("modifiant les directives 89/686/CEE"));
    }

    [Fact]
    public void Tables_become_one_list_item_per_row()
    {
        Assert.Contains(new Block(BlockKind.ListItem, "AIP 0102 Systèmes d'IA relevant de l'annexe I, section A, point 2", 1, "anx_XIV"), Blocks);
    }

    [Theory]
    [InlineData("▼")]                       // marqueurs de modification et de rectification
    [InlineData("►")]
    [InlineData("—————")]                   // trace d'une suppression
    [InlineData("RÈGLEMENT (UE) 2024/1689")] // titre, déjà en base
    [InlineData("Modifié par")]             // liste des actes modificatifs
    [InlineData("Rectifié par")]
    [InlineData("obligatoire dans tous ses éléments")] // formule finale
    public void Consolidation_furniture_is_not_extracted(string text)
    {
        Assert.DoesNotContain(Blocks, b => b.Text.Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Whitespace_is_normalized_but_text_is_otherwise_untouched()
    {
        Assert.All(Blocks, b => Assert.DoesNotMatch(@"\s{2}|^\s|\s$|\u00A0|\u202F", b.Text));
    }

    [Fact]
    public void Rejects_a_page_that_is_not_a_consolidated_act()
    {
        Assert.Throws<InvalidDataException>(() => Fixtures.Extract(new EurLexConsolidatedExtractor(), "ai-act-extraits.xhtml"));
    }

    [Theory]
    [InlineData("ai-act-extraits.xhtml", "eur-lex-xhtml/1")]
    [InlineData(Fixture, "eur-lex-consolide/1")]
    public void The_file_format_chooses_the_eur_lex_extractor(string fixture, string expected)
    {
        var raw = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture));

        Assert.Equal(expected, Extractors.For("eur-lex", raw).Version);
    }

    [Fact]
    public void An_unknown_source_has_no_extractor()
    {
        Assert.Throws<InvalidDataException>(() => Extractors.For("inconnue", Encoding.UTF8.GetBytes("<html/>")));
    }
}

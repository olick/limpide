using Limpide.Core.Extraction;
using Limpide.Core.Quality;

namespace Limpide.Core.Tests.Quality;

public class QualityValidatorTests
{
    private static readonly QualityOptions Options = new();

    private const string French =
        "Le fournisseur d'un système d'IA à haut risque veille à ce que les personnes soient informées de l'utilisation "
        + "du système et des droits dont elles disposent, dans une forme claire et accessible pour tous. ";

    /// <summary>Une fiche CNIL saine : assez de texte, en français, des intertitres, des passages de longueur visée.</summary>
    private static QualityInput Healthy(int? publishedPassageCount = 10)
    {
        var blocks = new List<Block> { new(BlockKind.Heading, "IA : informer les personnes", 1) };
        for (var i = 0; i < 5; i++)
        {
            blocks.Add(new Block(BlockKind.Heading, $"Section {i}", 2));
            blocks.Add(new Block(BlockKind.Paragraph, string.Concat(Enumerable.Repeat(French, 4))));
        }

        var passages = Enumerable.Range(0, 10)
            .Select(i => new PassageText($"Passage {i}. " + string.Concat(Enumerable.Repeat(French, 4)), null))
            .ToList();
        return new QualityInput("cnil", blocks, passages, publishedPassageCount);
    }

    private static QualityCheck Check(QualityInput input, string name) =>
        QualityValidator.Validate(input, Options).Checks.Single(c => c.Name == name);

    [Fact]
    public void A_healthy_version_passes_every_check()
    {
        var report = QualityValidator.Validate(Healthy(), Options);
        Assert.True(report.Passed, report.Summary());
        Assert.Equal(8, report.Checks.Count);
    }

    [Fact]
    public void An_error_page_has_too_little_text()
    {
        var input = Healthy() with { Blocks = [new(BlockKind.Paragraph, "Page introuvable. Retour à l'accueil.")] };
        Assert.False(Check(input, "texte-minimal").Passed);
    }

    [Fact]
    public void An_english_page_is_not_french()
    {
        var english = string.Concat(Enumerable.Repeat("The provider of a high-risk AI system shall ensure that the persons are informed of the use of the system. ", 30));
        var input = Healthy() with { Blocks = [new(BlockKind.Paragraph, english)] };

        var check = Check(input, "langue-francaise");
        Assert.False(check.Passed);
        Assert.Equal(0, check.Value);
        Assert.True(Check(Healthy(), "langue-francaise").Value > 0.3);
    }

    [Theory]
    [InlineData(10, true)]  // même nombre
    [InlineData(13, true)]  // +30 % : à la limite
    [InlineData(6, false)]  // -40 %
    [InlineData(20, false)] // +100 %
    public void The_passage_count_stays_close_to_the_published_version(int published, bool passes) =>
        Assert.Equal(passes, Check(Healthy(published), "nombre-de-passages").Passed);

    [Fact]
    public void A_first_version_has_nothing_to_compare_with()
    {
        var check = Check(Healthy(publishedPassageCount: null), "nombre-de-passages");
        Assert.True(check.Passed);
        Assert.Equal("sans version publiée à comparer", check.Detail);
    }

    [Fact]
    public void Too_many_passages_out_of_bounds_fail()
    {
        var input = Healthy();
        var passages = input.Passages.ToList();
        for (var i = 0; i < 3; i++)
            passages[i] = passages[i] with { Content = "Trop court." };

        var check = Check(input with { Passages = passages }, "longueur-des-passages");
        Assert.False(check.Passed);
        Assert.Equal(0.3, check.Value);
    }

    [Fact]
    public void Repeated_passages_are_duplicates()
    {
        var input = Healthy();
        var passages = input.Passages.ToList();
        passages[1] = passages[0];

        var check = Check(input with { Passages = passages }, "doublons");
        Assert.False(check.Passed);
        Assert.StartsWith("ex. « Passage 0.", check.Detail);
    }

    [Fact]
    public void A_cnil_page_without_sections_has_lost_its_structure()
    {
        var input = Healthy() with { Blocks = [new(BlockKind.Paragraph, string.Concat(Enumerable.Repeat(French, 20)))] };
        Assert.False(Check(input, "structure").Passed);
    }

    [Fact]
    public void The_ai_act_needs_its_articles()
    {
        var articles = Enumerable.Range(1, 113).Select(i => new PassageText("Article. " + French, $"art_{i}")).ToList();
        var input = Healthy() with { SourceSlug = "eur-lex", Passages = articles, PublishedPassageCount = 113 };
        Assert.True(Check(input, "structure").Passed);

        Assert.False(Check(input with { Passages = articles.Take(40).ToList() }, "structure").Passed);
    }

    [Theory]
    [InlineData("Voir <a href=\"/fr\">la fiche</a>.", false)]
    [InlineData("Montant&nbsp;: 15 millions d'euros.", false)]
    [InlineData("Si x < y et z > w, la règle s'applique.", true)]
    [InlineData("Le seuil est fixé à < 10 %.", true)]
    public void Html_residue_is_detected_without_false_alarms_on_comparisons(string text, bool passes)
    {
        var input = Healthy();
        var passages = input.Passages.ToList();
        passages[0] = passages[0] with { Content = passages[0].Content + text };
        Assert.Equal(passes, Check(input with { Passages = passages }, "residus-html").Passed);
    }

    [Theory]
    [InlineData("Accepter les cookies", false)]
    [InlineData("Aller au contenu", false)]
    [InlineData("Les cookies de mesure d'audience sont soumis au consentement.", true)]
    public void Interface_text_is_detected_but_cookies_as_a_topic_are_not(string text, bool passes)
    {
        var input = Healthy();
        var passages = input.Passages.ToList();
        passages[0] = passages[0] with { Content = passages[0].Content + text };
        Assert.Equal(passes, Check(input with { Passages = passages }, "texte-d-interface").Passed);
    }

    [Fact]
    public void The_summary_names_each_failure_with_its_rule_and_value()
    {
        var input = Healthy() with { Blocks = [new(BlockKind.Paragraph, "Page introuvable.")] };
        var summary = QualityValidator.Validate(input, Options).Summary();

        Assert.Contains("texte-minimal : au moins 2000 caractères de texte extrait, mesuré 17", summary);
        Assert.Contains("langue-francaise", summary);
    }
}

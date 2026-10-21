using Limpide.Core.Answering;
using Limpide.Core.Protection;

namespace Limpide.Core.Tests.Protection;

public class PersonalDataMaskTests
{
    [Theory]
    [InlineData("Écrivez-moi à jean.dupont@exemple.fr pour la suite", "Écrivez-moi à [adresse électronique] pour la suite")]
    [InlineData("contact : Prénom.Nom+ia@sous.domaine.com.", "contact : [adresse électronique].")]
    public void Email_addresses_are_masked(string text, string expected)
    {
        var masked = PersonalDataMask.Mask(text);

        Assert.Equal(expected, masked.Text);
        Assert.Equal([PersonalDataMask.Email], masked.Masked);
    }

    [Theory]
    [InlineData("Appelez le 06 12 34 56 78 demain")]
    [InlineData("Appelez le 0612345678 demain")]
    [InlineData("Appelez le 06.12.34.56.78 demain")]
    [InlineData("Appelez le +33 6 12 34 56 78 demain")]
    [InlineData("Appelez le +33 (0)6 12 34 56 78 demain")]
    [InlineData("Appelez le 0033 6 12 34 56 78 demain")]
    [InlineData("Appelez le +49 30 1234 5678 demain")]
    public void Phone_numbers_are_masked(string text)
    {
        var masked = PersonalDataMask.Mask(text);

        Assert.Equal("Appelez le [téléphone] demain", masked.Text);
        Assert.Equal([PersonalDataMask.Phone], masked.Masked);
    }

    [Theory]
    [InlineData("Mon numéro de sécurité sociale 1 85 05 78 006 084 36 est-il une donnée sensible ?")]
    [InlineData("Mon numéro de sécurité sociale 185057800608436 est-il une donnée sensible ?")]
    [InlineData("Mon numéro de sécurité sociale 2 90 12 2A 004 117 est-il une donnée sensible ?")]
    public void Social_security_numbers_are_masked(string text)
    {
        var masked = PersonalDataMask.Mask(text);

        Assert.Equal("Mon numéro de sécurité sociale [numéro de sécurité sociale] est-il une donnée sensible ?", masked.Text);
        Assert.Equal([PersonalDataMask.SocialSecurityNumber], masked.Masked);
    }

    [Theory]
    [InlineData("IBAN FR76 3000 6000 0112 3456 7890 189 sur ma facture")]
    [InlineData("IBAN FR7630006000011234567890189 sur ma facture")]
    public void Ibans_are_masked(string text)
    {
        var masked = PersonalDataMask.Mask(text);

        Assert.Equal("IBAN [IBAN] sur ma facture", masked.Text);
        Assert.Equal([PersonalDataMask.Iban], masked.Masked);
    }

    [Fact]
    public void Several_kinds_are_reported_once_each()
    {
        var masked = PersonalDataMask.Mask("a@b.fr, c@d.fr ou 06 12 34 56 78");

        Assert.Equal("[adresse électronique], [adresse électronique] ou [téléphone]", masked.Text);
        Assert.Equal([PersonalDataMask.Email, PersonalDataMask.Phone], masked.Masked);
    }

    // Les questions ordinaires de la démo citent des articles, des règlements, des dates et des montants : rien ne
    // doit y être masqué.
    [Theory]
    [InlineData("Que dit l'article 50 du règlement (UE) 2024/1689 ?")]
    [InlineData("Quelles obligations s'appliquent à partir du 2 août 2026 ?")]
    [InlineData("L'amende peut-elle atteindre 35 000 000 euros ou 7 % du chiffre d'affaires ?")]
    [InlineData("Le considérant 133 et l'annexe III, point 4, s'appliquent-ils ?")]
    [InlineData("Mon modèle GPT4 de 2025 est-il un modèle à usage général ?")]
    [InlineData("Délibération n° 2024-012 du 15 février 2024 de la CNIL")]
    [InlineData("Un système de 10 000 000 000 000 000 000 000 000 FLOP est-il à risque systémique ?")]
    public void Ordinary_questions_are_left_unchanged(string question)
    {
        var masked = PersonalDataMask.Mask(question);

        Assert.Equal(question, masked.Text);
        Assert.Empty(masked.Masked);
    }

    [Fact]
    public void A_kept_question_is_trimmed_and_masked()
    {
        var kept = KeptQuestion.From("  Puis-je vous écrire à moi@exemple.fr ?  ");

        Assert.Equal("Puis-je vous écrire à [adresse électronique] ?", kept.Text);
        Assert.Equal([PersonalDataMask.Email], kept.Masked);
    }
}

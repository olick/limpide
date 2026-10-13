using Limpide.Core.Answering;

namespace Limpide.Core.Tests.Answering;

public class ModelTextTests
{
    [Fact]
    public void Retire_l_espace_de_largeur_nulle_dans_une_citation()
    {
        var clean = ModelText.Clean("Documentation technique [​P5].");

        Assert.Equal("Documentation technique [P5].", clean);
        Assert.Equal([5], Citations.Check(clean, 5).Cited);
    }

    [Fact]
    public void Garde_le_texte_visible_intact()
    {
        Assert.Equal("Article 5 — « pratiques interdites » [P1]", ModelText.Clean("  Article 5 — « pratiques interdites » [P1]\n"));
    }
}

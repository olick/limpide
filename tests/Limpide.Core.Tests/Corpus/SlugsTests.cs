using Limpide.Core.Corpus;

namespace Limpide.Core.Tests.Corpus;

public class SlugsTests
{
    [Theory]
    [InlineData("EUR-Lex", "eur-lex")]
    [InlineData("CNIL", "cnil")]
    [InlineData("  Journal officiel (UE)  ", "journal-officiel-ue")]
    [InlineData("Légifrance", "legifrance")]
    public void From_produces_a_folder_safe_name(string name, string expected)
    {
        Assert.Equal(expected, Slugs.From(name));
    }
}

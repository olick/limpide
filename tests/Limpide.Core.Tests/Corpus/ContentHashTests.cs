using System.Text;
using Limpide.Core.Corpus;

namespace Limpide.Core.Tests.Corpus;

public class ContentHashTests
{
    [Fact]
    public void Compute_returns_lowercase_sha256_hex()
    {
        // Vecteur de test officiel SHA-256 (FIPS 180-2).
        var hash = ContentHash.Compute(Encoding.ASCII.GetBytes("abc"));

        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
    }

    [Fact]
    public void Compute_fits_the_char64_column()
    {
        Assert.Equal(64, ContentHash.Compute([]).Length);
    }
}

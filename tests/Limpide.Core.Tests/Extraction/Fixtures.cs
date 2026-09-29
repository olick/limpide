using Limpide.Core.Extraction;

namespace Limpide.Core.Tests.Extraction;

/// <summary>Extraits réels des sources, dans tests/Limpide.Core.Tests/Fixtures (licences en tête de fichier).</summary>
internal static class Fixtures
{
    public static IReadOnlyList<Block> Extract(IExtractor extractor, string fileName)
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
        return extractor.Extract(stream);
    }
}

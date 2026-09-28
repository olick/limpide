using System.Security.Cryptography;

namespace Limpide.Core.Corpus;

public static class ContentHash
{
    /// <summary>Empreinte SHA-256 du contenu brut, en hexadécimal minuscule (64 caractères).</summary>
    public static string Compute(ReadOnlySpan<byte> content) =>
        Convert.ToHexStringLower(SHA256.HashData(content));
}

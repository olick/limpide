using Limpide.Core.Extraction;

namespace Limpide.Core.Chunking;

/// <summary>
/// Découpe structurel : une unité de sens de la source (article, section) donne un passage,
/// redécoupé seulement s'il dépasse <see cref="Packer.MaxLength"/>. Le texte n'est jamais modifié.
/// </summary>
public interface IChunker
{
    /// <summary>Nom et version, à incrémenter à chaque changement des passages produits.</summary>
    string Version { get; }

    IReadOnlyList<Chunk> Split(IReadOnlyList<Block> blocks);
}

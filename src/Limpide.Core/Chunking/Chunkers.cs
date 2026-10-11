namespace Limpide.Core.Chunking;

/// <summary>Choix du découpeur selon la source (nom de dossier sous data/raw).</summary>
public static class Chunkers
{
    private static readonly Dictionary<string, IChunker> BySourceSlug = new()
    {
        ["eur-lex"] = new AiActChunker(),
        ["cnil"] = new CnilChunker(),
    };

    /// <param name="fixedSize">Découpage à taille fixe au lieu du découpage structurel : pour comparaison seulement.</param>
    public static IChunker ForSource(string sourceSlug, bool fixedSize = false) =>
        fixedSize ? new FixedSizeChunker(sourceSlug)
        : BySourceSlug.TryGetValue(sourceSlug, out var chunker)
            ? chunker
            : throw new InvalidDataException($"aucun découpeur pour la source « {sourceSlug} »");
}

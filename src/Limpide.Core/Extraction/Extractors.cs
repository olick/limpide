namespace Limpide.Core.Extraction;

/// <summary>Choix de l'extracteur selon la source (nom de dossier sous data/raw).</summary>
public static class Extractors
{
    private static readonly Dictionary<string, IExtractor> BySourceSlug = new()
    {
        ["eur-lex"] = new EurLexExtractor(),
        ["cnil"] = new CnilExtractor(),
    };

    public static IExtractor ForSource(string sourceSlug) =>
        BySourceSlug.TryGetValue(sourceSlug, out var extractor)
            ? extractor
            : throw new InvalidDataException($"aucun extracteur pour la source « {sourceSlug} »");
}

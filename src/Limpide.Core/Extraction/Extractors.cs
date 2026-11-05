using System.Text;

namespace Limpide.Core.Extraction;

/// <summary>Choix de l'extracteur selon la source (nom de dossier sous data/raw) et, pour EUR-Lex, le format du fichier.</summary>
public static class Extractors
{
    private static readonly EurLexExtractor OfficialJournal = new();
    private static readonly EurLexConsolidatedExtractor Consolidated = new();
    private static readonly CnilExtractor Cnil = new();

    /// <summary>
    /// EUR-Lex sert deux formats sous la même source : le Journal officiel (texte d'origine) et la version consolidée
    /// (texte en vigueur, modifications intégrées). Chacun a son extracteur et sa version, pour qu'un changement de
    /// l'un ne relance pas l'extraction de l'autre (ni, par suite, le découpage et la vectorisation de la version
    /// publiée).
    /// </summary>
    public static IExtractor For(string sourceSlug, byte[] raw) => sourceSlug switch
    {
        "eur-lex" => IsConsolidated(raw) ? Consolidated : OfficialJournal,
        "cnil" => Cnil,
        _ => throw new InvalidDataException($"aucun extracteur pour la source « {sourceSlug} »"),
    };

    private static bool IsConsolidated(byte[] raw) =>
        raw.AsSpan().IndexOf(Encoding.UTF8.GetBytes(EurLexConsolidatedExtractor.FormatMarker)) >= 0;
}

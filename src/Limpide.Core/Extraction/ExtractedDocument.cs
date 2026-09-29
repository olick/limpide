namespace Limpide.Core.Extraction;

/// <summary>
/// Texte structuré extrait d'un fichier brut : entrée de l'étape de découpage.
/// Sérialisé dans data/extracted/&lt;source&gt;/&lt;sha256&gt;.json.
/// </summary>
/// <param name="Extractor">Nom et version de l'extracteur (ex. « cnil-html/1 ») : une autre version impose de réextraire.</param>
/// <param name="ContentHash">Empreinte du fichier brut dont le texte est issu.</param>
public sealed record ExtractedDocument(string Extractor, string ContentHash, IReadOnlyList<Block> Blocks);

/// <summary>
/// Un bloc de texte tel qu'il figure dans la source, espaces normalisés.
/// </summary>
/// <param name="Level">Titre : niveau hiérarchique (1 = le plus haut). Élément de liste : profondeur (1 = premier niveau). Paragraphe : 0.</param>
/// <param name="Anchor">Identifiant de la subdivision d'origine quand la source en fournit (ex. « art_5 », « 005.001 »).</param>
public sealed record Block(BlockKind Kind, string Text, int Level = 0, string? Anchor = null);

public enum BlockKind
{
    Heading,
    Paragraph,
    ListItem,
}

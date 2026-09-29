namespace Limpide.Core.Extraction;

/// <summary>
/// Transforme un fichier brut en blocs de texte. Règle commune : ne produire que du texte présent
/// dans la source (licences CNIL et EUR-Lex), en retirant seulement l'habillage de la page.
/// </summary>
public interface IExtractor
{
    /// <summary>Nom et version, à incrémenter à chaque changement du résultat produit.</summary>
    string Version { get; }

    /// <summary>Lève <see cref="InvalidDataException"/> si la structure attendue est absente.</summary>
    IReadOnlyList<Block> Extract(Stream content);
}

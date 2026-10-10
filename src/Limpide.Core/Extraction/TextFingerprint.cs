using System.Text;
using Limpide.Core.Corpus;

namespace Limpide.Core.Extraction;

/// <summary>
/// Empreinte du texte extrait (blocs, dans l'ordre, avec leur nature, niveau et ancre). Deux versions d'un document
/// au même texte ont la même empreinte, même si leur fichier brut diffère : c'est le cas des pages de la CNIL, dont
/// le HTML change plusieurs fois par jour (identifiants techniques) sans que le texte change.
/// </summary>
public static class TextFingerprint
{
    public static string Compute(IEnumerable<Block> blocks)
    {
        var text = new StringBuilder();
        foreach (var block in blocks)
        {
            // Séparateurs absents du texte normalisé : deux découpages différents ne peuvent pas donner la même chaîne.
            text.Append(block.Kind).Append('\u001f').Append(block.Level).Append('\u001f')
                .Append(block.Anchor).Append('\u001f').Append(block.Text).Append('\u001e');
        }

        return ContentHash.Compute(Encoding.UTF8.GetBytes(text.ToString()));
    }
}

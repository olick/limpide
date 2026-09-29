using System.Text;
using System.Text.RegularExpressions;

namespace Limpide.Core.Chunking;

/// <summary>Un bloc de texte d'une unité, avec le groupe auquel il appartient (ex. paragraphe numéroté « 005.001 »).</summary>
public readonly record struct UnitBlock(string Text, string? Group);

/// <summary>
/// Répartit les blocs d'une unité (article, section) en passages de <see cref="MinLength"/> à <see cref="MaxLength"/>
/// caractères. Coupe de préférence entre deux groupes, sinon entre deux blocs, en dernier recours entre deux phrases ;
/// jamais au milieu d'une phrase. Une phrase plus longue que le maximum donne donc un passage plus long.
/// Commun à tous les découpeurs : toute modification ici impose d'incrémenter leurs versions.
/// </summary>
public static partial class Packer
{
    public const int MinLength = 500;
    public const int MaxLength = 1500;

    public static IReadOnlyList<string> Pack(IReadOnlyList<UnitBlock> blocks)
    {
        var pieces = new List<Piece>();
        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            if (block.Text.Length <= MaxLength)
                pieces.Add(new Piece(block.Text, i, block.Group));
            else
                pieces.AddRange(SentenceEnd().Split(block.Text).Select(sentence => new Piece(sentence, i, block.Group)));
        }

        if (pieces.Count == 0)
            return [];
        if (Length(pieces) <= MaxLength)
            return [Join(pieces)];

        var chunks = new List<List<Piece>>();
        var current = new List<Piece>();
        foreach (var piece in pieces)
        {
            if (current.Count > 0)
            {
                var tooLong = Length(current) + 1 + piece.Text.Length > MaxLength;
                var newGroupAndLongEnough = piece.Group != current[^1].Group && Length(current) >= MinLength;
                if (tooLong || newGroupAndLongEnough)
                {
                    chunks.Add(current);
                    current = [];
                }
            }

            current.Add(piece);
        }

        chunks.Add(current);

        // Un reste trop court rejoint le passage précédent, quitte à dépasser un peu le maximum.
        if (chunks.Count > 1 && Length(chunks[^1]) < MinLength && Length(chunks[^2]) + 1 + Length(chunks[^1]) <= MaxLength + MinLength)
        {
            chunks[^2].AddRange(chunks[^1]);
            chunks.RemoveAt(chunks.Count - 1);
        }

        return chunks.Select(Join).ToList();
    }

    /// <summary>Deux morceaux d'un même bloc sont séparés par une espace, deux blocs par un retour à la ligne.</summary>
    private static string Join(List<Piece> pieces)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < pieces.Count; i++)
        {
            if (i > 0)
                builder.Append(pieces[i].Block == pieces[i - 1].Block ? ' ' : '\n');
            builder.Append(pieces[i].Text);
        }

        return builder.ToString();
    }

    private static int Length(List<Piece> pieces) => pieces.Sum(p => p.Text.Length) + pieces.Count - 1;

    private readonly record struct Piece(string Text, int Block, string? Group);

    /// <summary>
    /// Fin de phrase : « . ! ? » suivi d'une majuscule, d'un guillemet ou d'une parenthèse,
    /// ou « ; » (fréquent entre deux alinéas d'un texte juridique). Pas de chiffre : « art. 5 » n'est pas une fin de phrase.
    /// </summary>
    [GeneratedRegex(@"(?<=[.!?])\s+(?=[\p{Lu}«(])|(?<=;)\s+")]
    private static partial Regex SentenceEnd();
}

using System.Text;
using System.Text.RegularExpressions;
using Limpide.Core.Extraction;

namespace Limpide.Core.Chunking;

/// <summary>
/// Découpage à taille fixe, pour comparaison (ADR sur la stratégie de découpage, S5) : le texte extrait, titres
/// compris, est coupé tous les <c>size</c> caractères environ (jamais dans un mot), avec <c>overlap</c> caractères de
/// chevauchement, sans tenir compte des articles ni des sections. C'est la méthode la plus répandue.
/// Pour une comparaison loyale, seule la coupe change : le titre de rattachement (vectorisé avec le texte) et l'ancre
/// sont ceux de l'endroit où commence le passage, calculés comme par les découpeurs structurels.
/// Non utilisé par défaut : <c>--Ingestion:ChunkingStrategy=FixedSize</c>.
/// </summary>
public sealed partial class FixedSizeChunker(string sourceSlug, int size = 1_000, int overlap = 200) : IChunker
{
    public string Version => $"taille-fixe-{size}-{overlap}/1";

    public IReadOnlyList<Chunk> Split(IReadOnlyList<Block> blocks)
    {
        // Texte continu, et pour chaque bloc sa position et son contexte (titre, ancre).
        var text = new StringBuilder();
        var starts = new List<(int Offset, string Heading, string? Anchor)>();
        var titles = new SortedDictionary<int, string>();
        string? unit = null;

        foreach (var block in blocks)
        {
            if (block.Kind == BlockKind.Heading)
            {
                foreach (var deeper in titles.Keys.Where(level => level >= block.Level).ToList())
                    titles.Remove(deeper);
                titles[block.Level] = block.Text;
            }

            if (sourceSlug == "eur-lex" && block.Anchor is { } anchor && UnitAnchor().IsMatch(anchor))
                unit = anchor;

            starts.Add((text.Length, Heading(titles, unit), sourceSlug == "eur-lex" ? unit : null));
            text.Append(block.Text).Append('\n');
        }

        var all = text.ToString();
        var chunks = new List<Chunk>();
        var start = 0;
        while (start < all.Length)
        {
            var end = Math.Min(start + size, all.Length);
            if (end < all.Length)
            {
                var space = all.LastIndexOfAny([' ', '\n'], end - 1, end - start);
                if (space > start)
                    end = space;
            }

            var content = all[start..end].Trim();
            if (content.Length > 0)
            {
                var context = starts.Last(s => s.Offset <= start);
                chunks.Add(new Chunk(context.Heading, content, context.Anchor));
            }

            if (end >= all.Length)
                break;

            // Reprise avec chevauchement, au début d'un mot.
            var next = Math.Max(end - overlap, start + 1);
            var word = all.IndexOfAny([' ', '\n'], next);
            start = word < 0 || word >= end ? end : word + 1;
        }

        return chunks;
    }

    private string Heading(SortedDictionary<int, string> titles, string? unit)
    {
        if (sourceSlug != "eur-lex")
            return string.Join(" › ", titles.Values);

        // Même titre que le découpeur de l'AI Act : l'article ou l'annexe, « Considérant (n) » pour un considérant.
        if (unit is not null && RecitalAnchor().Match(unit) is { Success: true } recital)
            return $"Considérant ({recital.Groups[1].Value})";
        return titles.Count == 0 ? "" : titles.Values.Last();
    }

    [GeneratedRegex(@"^(art_\d+|rct_\d+|anx_[IVXLC]+)$")]
    private static partial Regex UnitAnchor();

    [GeneratedRegex(@"^rct_(\d+)$")]
    private static partial Regex RecitalAnchor();
}

using Limpide.Core.Extraction;

namespace Limpide.Core.Chunking;

/// <summary>
/// Fiches CNIL : une unité par titre de section, quel que soit son niveau. Le titre de rattachement
/// reprend le chemin des titres (« IA : Annoter les données › Les enjeux… › Le principe de minimisation »).
/// Un paragraphe et la liste qui le suit forment un groupe : une section trop longue est coupée avant
/// un paragraphe plutôt qu'entre une phrase d'introduction et ses éléments de liste.
/// </summary>
public sealed class CnilChunker : IChunker
{
    private const string Separator = " › ";

    public string Version => "cnil-sections/1";

    public IReadOnlyList<Chunk> Split(IReadOnlyList<Block> blocks)
    {
        var chunks = new List<Chunk>();
        var titles = new SortedDictionary<int, string>();
        var content = new List<UnitBlock>();
        var group = 0;

        void Flush()
        {
            var heading = string.Join(Separator, titles.Values);
            chunks.AddRange(Packer.Pack(content).Select(text => new Chunk(heading, text, null)));
            content.Clear();
        }

        foreach (var block in blocks)
        {
            if (block.Kind == BlockKind.Heading)
            {
                Flush();
                foreach (var deeper in titles.Keys.Where(level => level >= block.Level).ToList())
                    titles.Remove(deeper);
                titles[block.Level] = block.Text;
            }
            else
            {
                if (block.Kind == BlockKind.Paragraph)
                    group++;
                content.Add(new UnitBlock(block.Text, group.ToString()));
            }
        }

        Flush();
        return chunks;
    }
}

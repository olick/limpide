using System.Text.RegularExpressions;
using Limpide.Core.Extraction;

namespace Limpide.Core.Chunking;

/// <summary>
/// Règlement européen balisé ELI (AI Act). Unités : un article, un considérant, une annexe, le préambule.
/// Un article trop long est redécoupé entre ses paragraphes numérotés, une annexe entre ses sous-titres.
/// Les titres de chapitre et de section ne servent qu'à délimiter : ils ne sont pas repris dans les passages.
/// </summary>
public sealed partial class AiActChunker : IChunker
{
    public string Version => "ai-act/1";

    public IReadOnlyList<Chunk> Split(IReadOnlyList<Block> blocks)
    {
        var chunks = new List<Chunk>();
        Unit? unit = null;
        var seenRecital = false;

        void Start(string heading, string anchor)
        {
            Flush();
            unit = new Unit(heading, anchor);
        }

        void Flush()
        {
            if (unit is not null)
                chunks.AddRange(Packer.Pack(unit.Blocks).Select(content => new Chunk(unit.Heading, content, unit.Anchor)));
            unit = null;
        }

        foreach (var block in blocks)
        {
            var anchor = block.Anchor ?? string.Empty;

            if (block.Kind == BlockKind.Heading)
            {
                if (ArticleId().IsMatch(anchor) || (AnnexId().IsMatch(anchor) && block.Level == 1))
                {
                    Start(block.Text, anchor);
                }
                else if (unit is not null && AnnexId().IsMatch(anchor))
                {
                    // Sous-titre d'annexe (« Section A. … ») : texte de la source, et limite de groupe.
                    unit.Group = block.Text;
                    unit.Blocks.Add(new UnitBlock(block.Text, unit.Group));
                }
                else
                {
                    Flush(); // chapitre ou section : l'article suivant ouvrira sa propre unité
                }

                continue;
            }

            seenRecital |= RecitalId().IsMatch(anchor);

            if (RecitalId().Match(anchor) is { Success: true } recital && unit?.Anchor != anchor)
                Start($"Considérant ({recital.Groups[1].Value})", anchor);
            else if (PreambleId().IsMatch(anchor) && seenRecital)
                continue; // « ONT ADOPTÉ LE PRÉSENT RÈGLEMENT: », formule entre les considérants et l'article premier
            else if (PreambleId().IsMatch(anchor) && unit?.Anchor != "pbl_1")
                Start("Préambule", "pbl_1");
            else if (unit is null)
                throw new InvalidDataException($"bloc hors de tout article, considérant ou annexe : « {Shorten(block.Text)} »");

            // Dans un article, un groupe par paragraphe numéroté (« 005.001 ») ; dans une annexe, par sous-titre.
            unit!.Blocks.Add(new UnitBlock(block.Text, AnnexId().IsMatch(unit.Anchor) ? unit.Group : anchor));
        }

        Flush();
        return chunks;
    }

    private static string Shorten(string text) => text.Length <= 60 ? text : text[..60] + "…";

    private sealed class Unit(string heading, string anchor)
    {
        public string Heading { get; } = heading;
        public string Anchor { get; } = anchor;
        public List<UnitBlock> Blocks { get; } = [];
        public string? Group { get; set; }
    }

    [GeneratedRegex(@"^art_\d+$")]
    private static partial Regex ArticleId();

    [GeneratedRegex(@"^anx_[IVXLC]+$")]
    private static partial Regex AnnexId();

    [GeneratedRegex(@"^rct_(\d+)$")]
    private static partial Regex RecitalId();

    [GeneratedRegex(@"^(pbl|cit)_\d+$")]
    private static partial Regex PreambleId();
}

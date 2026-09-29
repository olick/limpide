using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Limpide.Core.Extraction;

/// <summary>
/// Règlements au format XHTML de l'Office des publications (API CELLAR), balisés ELI :
/// chapitres « cpt_X », sections « cpt_X.sct_N », articles « art_N », paragraphes « 005.001 »,
/// considérants « rct_N », annexes « anx_X ».
/// Écarte l'en-tête et le pied du Journal officiel, le titre (déjà en base), les notes de bas de page,
/// la formule finale et les signatures.
/// </summary>
public sealed partial class EurLexExtractor : IExtractor
{
    public string Version => "eur-lex-xhtml/1";

    public IReadOnlyList<Block> Extract(Stream content)
    {
        var document = new HtmlParser().ParseDocument(content);
        if (document.Body is null || document.QuerySelector("div.eli-subdivision[id^='art_']") is null)
            throw new InvalidDataException("aucun article balisé ELI (div.eli-subdivision#art_N) : format CELLAR inattendu");

        // Appels de note « (4) » dans le texte ; les notes elles-mêmes sont ignorées plus bas.
        foreach (var call in document.QuerySelectorAll(".oj-note-tag").Select(tag => tag.Closest("a") ?? tag).ToList())
            RemoveNoteCall(call);

        // Directement sous body : en-tête et pied du Journal officiel (tableaux, paragraphes) autour
        // des div qui portent le texte de l'acte puis des annexes.
        var blocks = new List<Block>();
        foreach (var division in document.Body.Children.Where(c => c.LocalName == "div"))
            Visit(division, blocks);
        return blocks;
    }

    /// <summary>
    /// « européen (1), » devient « européen, » : l'espace qui précédait l'appel part avec lui
    /// quand une ponctuation suit, pour ne pas laisser « européen , ».
    /// </summary>
    private static void RemoveNoteCall(IElement call)
    {
        if (call.PreviousSibling is IText before
            && (call.NextSibling is not IText after || after.Data.Length == 0 || !char.IsLetterOrDigit(after.Data[0])))
        {
            before.Data = before.Data.TrimEnd();
        }

        call.Remove();
    }

    private static void VisitChildren(IElement parent, List<Block> blocks, params IElement?[] except)
    {
        foreach (var child in parent.Children)
        {
            if (!except.Contains(child))
                Visit(child, blocks);
        }
    }

    private static void Visit(IElement element, List<Block> blocks)
    {
        if (IsIgnored(element))
            return;

        var id = element.Id ?? string.Empty;

        if (DivisionId().IsMatch(id))
        {
            // Chapitre ou section : « CHAPITRE III » puis son intitulé.
            var level = id.Contains(".sct_") ? 2 : 1;
            var number = DirectChild(element, "p.oj-ti-section-1");
            var title = DirectChild(element, "div.eli-title");
            AddHeading(blocks, level, id, number, title);
            VisitChildren(element, blocks, number, title);
        }
        else if (ArticleId().IsMatch(id))
        {
            var number = DirectChild(element, "p.oj-ti-art");
            var title = DirectChild(element, "div.eli-title");
            AddHeading(blocks, 3, id, number, title);
            VisitChildren(element, blocks, number, title);
        }
        else if (AnnexId().IsMatch(id))
        {
            // « ANNEXE III » puis son intitulé, dans deux p.oj-doc-ti successifs.
            var titles = element.Children.Where(c => c.Matches("p.oj-doc-ti")).Take(2).ToArray();
            AddHeading(blocks, 1, id, titles);
            VisitChildren(element, blocks, titles);
        }
        else if (element.Matches("p.oj-ti-grseq-1"))
        {
            AddHeading(blocks, 2, AnchorOf(element), element);
        }
        else if (element.Matches("div.oj-enumeration-spacing"))
        {
            // Point d'annexe sur une ligne : « 1. » et son texte dans deux p en ligne.
            Add(blocks, BlockKind.ListItem, string.Join(" ", element.Children.Select(c => c.TextContent)), 1, AnchorOf(element));
        }
        else if (element.LocalName == "p")
        {
            Add(blocks, BlockKind.Paragraph, element.TextContent, 0, AnchorOf(element));
        }
        else if (element.LocalName == "table")
        {
            VisitList(element, 1, blocks);
        }
        else
        {
            VisitChildren(element, blocks);
        }
    }

    /// <summary>
    /// Les énumérations sont des tableaux : une cellule pour le repère (« a) », « (1) »),
    /// la dernière pour le texte, qui peut contenir un tableau imbriqué (sous-points « i) »).
    /// </summary>
    private static void VisitList(IElement table, int depth, List<Block> blocks)
    {
        var rows = table.Children
            .SelectMany(c => c.LocalName is "tbody" or "thead" ? c.Children : (IEnumerable<IElement>)[c])
            .Where(r => r.LocalName == "tr");

        foreach (var row in rows)
        {
            var cells = row.Children.Where(c => c.LocalName == "td").ToList();
            if (cells.Count == 0)
                continue;

            var label = Text.Normalize(string.Join(" ", cells[..^1].Select(c => c.TextContent)));
            var anchor = AnchorOf(row);
            var pending = new List<string>();
            var labelUsed = false;

            void Flush()
            {
                var text = Text.Normalize(string.Join(" ", pending));
                pending.Clear();
                if (text.Length == 0)
                    return;
                if (!labelUsed && label.Length > 0)
                    text = $"{label} {text}";
                labelUsed = true;
                Add(blocks, BlockKind.ListItem, text, depth, anchor);
            }

            foreach (var node in cells[^1].ChildNodes)
            {
                if (node is IElement { LocalName: "table" } nested)
                {
                    Flush();
                    VisitList(nested, depth + 1, blocks);
                }
                else if (node is IElement { LocalName: "p" or "div" } paragraph)
                {
                    Flush();
                    pending.Add(paragraph.TextContent);
                    Flush();
                }
                else
                {
                    pending.Add(node.TextContent);
                }
            }

            Flush();
        }
    }

    private static bool IsIgnored(IElement element) =>
        element.LocalName is "hr" or "col" or "script" or "style"
        || element.Matches("p.oj-note, div.oj-final, div.eli-main-title");

    private static IElement? DirectChild(IElement parent, string selector) =>
        parent.Children.FirstOrDefault(c => c.Matches(selector));

    private static void AddHeading(List<Block> blocks, int level, string? anchor, params IElement?[] parts)
    {
        var text = string.Join(" — ", parts.Select(p => Text.Normalize(p?.TextContent)).Where(t => t.Length > 0));
        Add(blocks, BlockKind.Heading, text, level, anchor);
    }

    private static void Add(List<Block> blocks, BlockKind kind, string text, int level, string? anchor)
    {
        text = Text.Normalize(text);
        if (text.Length > 0)
            blocks.Add(new Block(kind, text, level, anchor));
    }

    /// <summary>Identifiant ELI de la subdivision la plus proche : paragraphe, article, considérant, annexe...</summary>
    private static string? AnchorOf(IElement element)
    {
        for (var current = element; current is not null; current = current.ParentElement)
        {
            if (current.Id is { } id && AnchorId().IsMatch(id))
                return id;
        }

        return null;
    }

    [GeneratedRegex(@"^cpt_[IVXLC]+(\.sct_\d+)?$")]
    private static partial Regex DivisionId();

    [GeneratedRegex(@"^art_\d+$")]
    private static partial Regex ArticleId();

    [GeneratedRegex(@"^anx_[IVXLC]+$")]
    private static partial Regex AnnexId();

    [GeneratedRegex(@"^(art_\d+|\d{3}\.\d{3}|rct_\d+|cit_\d+|anx_[IVXLC]+|pbl_\d+|cpt_[IVXLC]+(\.sct_\d+)?)$")]
    private static partial Regex AnchorId();
}

using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Limpide.Core.Extraction;

/// <summary>
/// Fiches du site cnil.fr (Drupal). Le contenu est dans « main .ctn-gen » : titre, introduction,
/// corps et sections repliables. Écarte menus, outils de partage, date, fiches précédente/suivante,
/// mots-clés et suggestions. Images exclues (licence CC-BY-NC-ND, voir docs/sources.md).
/// </summary>
public sealed class CnilExtractor : IExtractor
{
    private const string Chrome =
        ".page--tools, .ctn-gen-push-3, .general-tags, .nav--hub-nav, .ctn-gen-auteur, " +
        "button.ctn-gen-ascenseur-collapse, .sr-only, .visually-hidden, " +
        "script, style, noscript, form, img, picture, figure, svg, iframe, video";

    private static readonly HashSet<string> Inline =
    [
        "a", "abbr", "b", "br", "button", "cite", "code", "em", "i", "mark", "q",
        "small", "span", "strong", "sub", "sup", "u", "time",
    ];

    public string Version => "cnil-html/1";

    public IReadOnlyList<Block> Extract(Stream content)
    {
        var document = new HtmlParser().ParseDocument(content);
        var root = document.QuerySelector("main .ctn-gen")
            ?? throw new InvalidDataException("bloc de contenu « main .ctn-gen » introuvable : structure de page CNIL inattendue");

        foreach (var element in root.QuerySelectorAll(Chrome).ToList())
            element.Remove();

        var blocks = new List<Block>();
        VisitContainer(root, blocks);
        return blocks;
    }

    /// <summary>Parcourt un conteneur ; le texte libre entre deux blocs devient un paragraphe.</summary>
    private static void VisitContainer(IElement container, List<Block> blocks)
    {
        var pending = new StringBuilder();

        void Flush()
        {
            Add(blocks, BlockKind.Paragraph, pending.ToString(), 0);
            pending.Clear();
        }

        foreach (var node in container.ChildNodes)
        {
            if (node is not IElement element)
            {
                pending.Append(node.TextContent);
                continue;
            }

            if (Inline.Contains(element.LocalName))
            {
                pending.Append(element.LocalName == "br" ? " " : element.TextContent);
                continue;
            }

            Flush();
            switch (element.LocalName)
            {
                case "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
                    Add(blocks, BlockKind.Heading, element.TextContent, element.LocalName[1] - '0');
                    break;
                case "p":
                    Add(blocks, BlockKind.Paragraph, element.TextContent, 0);
                    break;
                case "ul" or "ol":
                    VisitList(element, 1, blocks);
                    break;
                case "table":
                    foreach (var row in element.QuerySelectorAll("tr"))
                        Add(blocks, BlockKind.Paragraph, string.Join(" | ", row.Children.Select(c => Text.Normalize(c.TextContent))), 0);
                    break;
                case "hr":
                    break;
                default:
                    VisitContainer(element, blocks);
                    break;
            }
        }

        Flush();
    }

    private static void VisitList(IElement list, int depth, List<Block> blocks)
    {
        foreach (var item in list.Children.Where(c => c.LocalName == "li"))
        {
            var text = new StringBuilder();
            var nested = new List<IElement>();

            foreach (var node in item.ChildNodes)
            {
                if (node is IElement { LocalName: "ul" or "ol" } sublist)
                    nested.Add(sublist);
                else if (node is IElement element && !Inline.Contains(element.LocalName))
                    text.Append(' ').Append(element.TextContent).Append(' ');
                else
                    text.Append(node.TextContent);
            }

            Add(blocks, BlockKind.ListItem, text.ToString(), depth);
            foreach (var sublist in nested)
                VisitList(sublist, depth + 1, blocks);
        }
    }

    private static void Add(List<Block> blocks, BlockKind kind, string text, int level)
    {
        text = Text.Normalize(text);
        if (text.Length > 0)
            blocks.Add(new Block(kind, text, level));
    }
}

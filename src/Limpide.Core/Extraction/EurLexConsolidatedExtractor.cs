using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Limpide.Core.Extraction;

/// <summary>
/// Version consolidée d'un règlement (API CELLAR, identifiant CELEX « 0AAAAR… »), au format HTML d'EUR-Lex, pas au
/// format du Journal officiel : chapitres « cpt_X », sections « cpt_X.sct_N », articles « art_N » ou « art_Na »
/// (article inséré, « 4 bis »), annexes « anx_X ». Les paragraphes n'ont pas d'identifiant : leur ancre est
/// reconstituée selon le schéma ELI du Journal officiel (« 005.001 » : article 5, paragraphe 1).
/// Une version consolidée ne reprend ni les considérants ni les signatures.
/// Écarte l'en-tête (liste des actes modificatifs), le titre (déjà en base), les marqueurs de modification
/// (« ▼M1 », « ▼B », « ▼C1 » et les tirets d'une suppression), les appels et notes de bas de page, la formule finale.
/// </summary>
public sealed partial class EurLexConsolidatedExtractor : IExtractor
{
    public string Version => "eur-lex-consolide/1";

    /// <summary>Classe propre au format consolidé : sert à le distinguer du format du Journal officiel.</summary>
    public const string FormatMarker = "title-article-norm";

    public IReadOnlyList<Block> Extract(Stream content)
    {
        var document = new HtmlParser().ParseDocument(content);
        var container = document.QuerySelector("div.eli-container");
        if (container is null || container.QuerySelector("div.eli-subdivision[id^='art_']") is null)
            throw new InvalidDataException("aucun article balisé ELI (div.eli-container, div#art_N) : format consolidé inattendu");

        foreach (var call in container.QuerySelectorAll("a[href^='#E']").ToList())
            RemoveNoteCall(call);

        var blocks = new List<Block>();
        VisitChildren(container, blocks);
        return blocks;
    }

    /// <summary>
    /// « du Conseil (1) et » devient « du Conseil et », « européen (1), » devient « européen, » : l'appel est un lien
    /// entre deux parenthèses portées par le texte qui l'entoure.
    /// </summary>
    private static void RemoveNoteCall(IElement call)
    {
        if (call.PreviousSibling is IText before && before.Data.TrimEnd().EndsWith('('))
            before.Data = before.Data.TrimEnd()[..^1];
        if (call.NextSibling is IText after && after.Data.TrimStart().StartsWith(')'))
            after.Data = after.Data.TrimStart()[1..];

        if (call.PreviousSibling is IText previous
            && (call.NextSibling is not IText next || next.Data.Length == 0 || !char.IsLetterOrDigit(next.Data[0])))
        {
            previous.Data = previous.Data.TrimEnd();
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
            var number = DirectChild(element, "p.title-division-1");
            var title = DirectChild(element, "p.title-division-2");
            AddHeading(blocks, level, id, number, title);
            VisitChildren(element, blocks, number, title);
        }
        else if (ArticleId().Match(id) is { Success: true } article)
        {
            var number = DirectChild(element, "p.title-article-norm");
            var title = DirectChild(element, "div.eli-title");
            AddHeading(blocks, 3, id, number, title);
            VisitArticle(element, article, blocks, number, title);
        }
        else if (AnnexId().IsMatch(id))
        {
            // « ANNEXE III » puis son intitulé : p.title-annex-2 d'ordinaire ; à défaut, le p qui suit le numéro
            // (annexe X : second p.title-annex-1 ; annexe XIV, insérée par l'Omnibus : p.norm).
            var number = DirectChild(element, "p.title-annex-1");
            var title = DirectChild(element, "p.title-annex-2")
                ?? (number?.NextElementSibling is { LocalName: "p" } next && !next.Matches("p.modref, p.title-gr-seq-level-1") ? next : null);
            AddHeading(blocks, 1, id, number, title);
            VisitChildren(element, blocks, number, title);
        }
        else if (element.Matches("p.title-gr-seq-level-1"))
        {
            AddHeading(blocks, 2, AnchorOf(element), element);
        }
        else if (element.Matches("p.title-gr-seq-level-2"))
        {
            AddHeading(blocks, 3, AnchorOf(element), element);
        }
        else if (element.Matches("div.grid-list"))
        {
            VisitListItem(element, 1, AnchorOf(element), blocks);
        }
        else if (element.LocalName == "p")
        {
            Add(blocks, BlockKind.Paragraph, element.TextContent, 0, AnchorOf(element));
        }
        else if (element.LocalName == "table")
        {
            VisitTable(element, AnchorOf(element), blocks);
        }
        else
        {
            VisitChildren(element, blocks);
        }
    }

    /// <summary>
    /// Paragraphes numérotés d'un article : « 1. » dans un span, le texte à côté (texte seul, ou alinéas et
    /// énumérations). Le numéro précède le premier bloc du paragraphe, comme au Journal officiel.
    /// Les alinéas suivants d'un paragraphe sont ses frères, pas ses enfants : ils gardent son ancre jusqu'au
    /// paragraphe numéroté suivant (le Journal officiel les range dans la subdivision du paragraphe).
    /// </summary>
    private static void VisitArticle(IElement article, Match id, List<Block> blocks, params IElement?[] except)
    {
        var current = id.Value;
        foreach (var child in article.Children)
        {
            if (except.Contains(child) || IsIgnored(child))
                continue;

            var label = child.Matches("div.norm") ? DirectChild(child, "span.no-parag") : null;
            if (label is null)
            {
                // Alinéa non numéroté, ou énumération directement sous l'article.
                if (child.Matches("div.grid-list"))
                    VisitListItem(child, 1, current, blocks);
                else
                    VisitContent(child, current, string.Empty, blocks);
                continue;
            }

            var number = Text.Normalize(label.TextContent);
            current = ParagraphAnchor(id, number);
            var body = child.Children.FirstOrDefault(c => c != label) ?? child;
            VisitContent(body, current, number, blocks);
        }
    }

    /// <summary>Contenu d'un paragraphe : texte direct, alinéas p, énumérations ; le libellé précède le premier bloc.</summary>
    private static void VisitContent(IElement body, string anchor, string label, List<Block> blocks)
    {
        var pending = new List<string>();
        var labelUsed = label.Length == 0;

        void Flush()
        {
            // Morceaux d'une même phrase (texte et éléments en ligne) : leurs espaces sont déjà dans la source.
            var text = Text.Normalize(string.Concat(pending));
            pending.Clear();
            if (text.Length == 0)
                return;
            if (!labelUsed)
                text = $"{label} {text}";
            labelUsed = true;
            Add(blocks, BlockKind.Paragraph, text, 0, anchor);
        }

        // Les éléments en ligne (italique, lien) font partie de la phrase ; p, div et table la terminent.
        void Walk(IElement container)
        {
            foreach (var node in container.ChildNodes)
            {
                if (node is not IElement element)
                {
                    pending.Add(node.TextContent);
                    continue;
                }

                if (IsIgnored(element))
                    continue;

                if (element.Matches("div.grid-list"))
                {
                    Flush();
                    if (!labelUsed)
                    {
                        // Énumération en tête de paragraphe, sans phrase d'introduction : le numéro reste seul.
                        Add(blocks, BlockKind.Paragraph, label, 0, anchor);
                        labelUsed = true;
                    }

                    VisitListItem(element, 1, anchor, blocks);
                }
                else if (element.LocalName == "table")
                {
                    Flush();
                    VisitTable(element, anchor, blocks);
                }
                else if (element.LocalName is "p" or "div")
                {
                    Flush();
                    Walk(element);
                    Flush();
                }
                else
                {
                    pending.Add(element.TextContent);
                }
            }
        }

        Walk(body);
        Flush();
    }

    /// <summary>
    /// Point d'énumération : repère (« a) », « 1. ») dans la première colonne, texte dans la seconde, qui peut contenir
    /// plusieurs alinéas et une énumération imbriquée (sous-points « i) »).
    /// </summary>
    private static void VisitListItem(IElement item, int depth, string? anchor, List<Block> blocks)
    {
        var label = Text.Normalize(DirectChild(item, "div.grid-list-column-1")?.TextContent);
        var content = DirectChild(item, "div.grid-list-column-2");
        if (content is null)
            return;

        var labelUsed = label.Length == 0;
        foreach (var child in content.Children)
        {
            if (IsIgnored(child))
                continue;

            if (child.Matches("div.grid-list"))
            {
                if (!labelUsed)
                {
                    Add(blocks, BlockKind.ListItem, label, depth, anchor);
                    labelUsed = true;
                }

                VisitListItem(child, depth + 1, anchor, blocks);
            }
            else if (child.LocalName == "table")
            {
                VisitTable(child, anchor, blocks);
            }
            else
            {
                var text = Text.Normalize(child.TextContent);
                if (text.Length == 0)
                    continue;
                if (!labelUsed)
                    text = $"{label} {text}";
                labelUsed = true;
                Add(blocks, BlockKind.ListItem, text, depth, anchor);
            }
        }
    }

    /// <summary>Tableau (annexe XIV : codes et intitulés) : une ligne par élément de liste, cellules à la suite.</summary>
    private static void VisitTable(IElement table, string? anchor, List<Block> blocks)
    {
        foreach (var row in table.QuerySelectorAll("tr"))
        {
            var text = string.Join(" ", row.Children.Where(c => c.LocalName == "td").Select(c => c.TextContent));
            Add(blocks, BlockKind.ListItem, text, 1, anchor);
        }
    }

    private static bool IsIgnored(IElement element) =>
        element.LocalName is "hr" or "col" or "br" or "script" or "style"
        || element.Matches("p.modref, p.arrow, p.footnote, div.eli-main-title")
        || (element.Id ?? string.Empty).StartsWith("fnp_", StringComparison.Ordinal);

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

    /// <summary>
    /// « art_5 » et « 1. » donnent « 005.001 », « art_4a » et « 2. » donnent « 004a.002 », « art_6 » et « 2 bis »
    /// donnent « 006.002bis » ; tout autre repère (texte cité entre guillemets d'un acte modifié) : l'article.
    /// </summary>
    private static string ParagraphAnchor(Match article, string label)
    {
        var number = ParagraphNumber().Match(label);
        if (!number.Success)
            return article.Value;

        return $"{int.Parse(article.Groups[1].Value):D3}{article.Groups[2].Value}"
            + $".{int.Parse(number.Groups[1].Value):D3}{number.Groups[2].Value}";
    }

    /// <summary>Article ou annexe le plus proche (les paragraphes n'ont pas d'identifiant dans ce format).</summary>
    private static string? AnchorOf(IElement element)
    {
        for (var current = element; current is not null; current = current.ParentElement)
        {
            if (current.Id is { } id && (ArticleId().IsMatch(id) || AnnexId().IsMatch(id) || DivisionId().IsMatch(id)))
                return id;
        }

        return null;
    }

    [GeneratedRegex(@"^cpt_[IVXLC]+(\.sct_\d+)?$")]
    private static partial Regex DivisionId();

    [GeneratedRegex(@"^art_(\d+)([a-z]?)$")]
    private static partial Regex ArticleId();

    [GeneratedRegex(@"^anx_[IVXLC]+$")]
    private static partial Regex AnnexId();

    [GeneratedRegex(@"^(\d+)(?:\s*(bis|ter|quater|quinquies|sexies))?\s*\.?$")]
    private static partial Regex ParagraphNumber();
}

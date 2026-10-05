using System.Text.RegularExpressions;
using Markdig;
using Markdig.Parsers.Inlines;

namespace Limpide.Web;

/// <summary>
/// Affiche la réponse du modèle (Markdown : gras, listes). Le texte vient d'un LLM, donc d'une source non fiable
/// (une question piégée peut lui faire écrire n'importe quoi) :
/// - HTML brut désactivé : pas de &lt;script&gt; ni d'attribut onerror ;
/// - liens, images et liens automatiques désactivés : sinon [x](javascript:...) produirait un lien exécutable,
///   et une image pourrait appeler un site tiers. Une réponse n'a pas à contenir de lien.
/// Les seuls liens sont ceux qu'on ajoute après coup : les citations [P1] (et [P1a]) vers l'extrait cité.
/// </summary>
public static partial class AnswerMarkdown
{
    private static readonly MarkdownPipeline Pipeline = CreatePipeline();

    private static MarkdownPipeline CreatePipeline()
    {
        var builder = new MarkdownPipelineBuilder().DisableHtml();
        builder.InlineParsers.RemoveAll(parser => parser is LinkInlineParser);
        builder.InlineParsers.RemoveAll(parser => parser is AutolinkInlineParser);
        return builder.Build();
    }

    public static string ToHtml(string answer, IReadOnlySet<string> citedIds) =>
        Citation().Replace(Markdown.ToHtml(answer, Pipeline), match =>
            citedIds.Contains(match.Groups[1].Value)
                ? $"""<a class="cite" href="#{match.Groups[1].Value}">{match.Value}</a>"""
                : match.Value);

    [GeneratedRegex(@"\[(P\d+)[a-z]{0,4}\]")]
    private static partial Regex Citation();
}

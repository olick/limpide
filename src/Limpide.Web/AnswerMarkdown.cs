using Limpide.Core.Answering;
using Markdig;
using Markdig.Parsers.Inlines;

namespace Limpide.Web;

/// <summary>
/// Affiche la réponse du modèle (Markdown : gras, listes). Le texte vient d'un LLM, donc d'une source non fiable
/// (une question piégée peut lui faire écrire n'importe quoi) :
/// - HTML brut désactivé : pas de &lt;script&gt; ni d'attribut onerror ;
/// - liens, images et liens automatiques désactivés : sinon [x](javascript:...) produirait un lien exécutable,
///   et une image pourrait appeler un site tiers. Une réponse n'a pas à contenir de lien.
/// Les seuls liens sont ceux qu'on ajoute après coup : les citations ([P1], [P6.3]…, voir Citations.Link) vers
/// l'extrait cité, qu'elles ouvrent (limpide.js).
/// </summary>
public static class AnswerMarkdown
{
    private static readonly MarkdownPipeline Pipeline = CreatePipeline();

    private static MarkdownPipeline CreatePipeline()
    {
        var builder = new MarkdownPipelineBuilder().DisableHtml();
        builder.InlineParsers.RemoveAll(parser => parser is LinkInlineParser);
        builder.InlineParsers.RemoveAll(parser => parser is AutolinkInlineParser);
        return builder.Build();
    }

    /// <remarks>
    /// Le nom du lien commence par le texte affiché (WCAG 2.5.3 : commande vocale, précision « .3 » du renvoi). Ce texte
    /// vient du HTML de Markdig, déjà échappé et sans balise (Citations.Link) : sûr dans un attribut.
    /// </remarks>
    /// <param name="anchorPrefix">Préfixe des ancres (« q2- ») : plusieurs réponses du fil citent chacune leur P1.</param>
    public static string ToHtml(string answer, IReadOnlySet<string> citedIds, string anchorPrefix = "") =>
        Citations.Link(Markdown.ToHtml(answer, Pipeline), (id, text) => citedIds.Contains(id)
            ? $"""<a class="cite" href="#{anchorPrefix}{id}" aria-label="{text} : ouvrir le texte cité">{text}</a>"""
            : null);
}

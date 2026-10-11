using System.Text.RegularExpressions;
using Limpide.Core.Extraction;

namespace Limpide.Core.Quality;

/// <summary>Passage produit par le découpage, tel que les contrôles le lisent.</summary>
public sealed record PassageText(string Content, string? Anchor);

/// <summary>Ce qu'on contrôle : le texte extrait d'une version, ses passages, et la version publiée du document.</summary>
/// <param name="SourceSlug">« eur-lex » ou « cnil » : choisit le contrôle de structure.</param>
/// <param name="PublishedPassageCount">Passages de la version publiée ; null pour une première version.</param>
public sealed record QualityInput(
    string SourceSlug, IReadOnlyList<Block> Blocks, IReadOnlyList<PassageText> Passages, int? PublishedPassageCount);

/// <summary>
/// Contrôles qualité d'une version, entre le découpage et la vectorisation : une version qui échoue n'est pas
/// publiée (quarantaine), la version publiée reste en service. Contrôles déterministes, sans IA, chacun avec son seuil.
/// </summary>
public static partial class QualityValidator
{
    // Mots outils : très fréquents, propres à une langue, présents dans tout texte suivi.
    private static readonly HashSet<string> FrenchFunctionWords =
    [
        "le", "la", "les", "de", "des", "du", "un", "une", "et", "est", "en", "à", "au", "aux", "pour", "par", "sur",
        "dans", "que", "qui", "ne", "pas", "ce", "cette", "ces", "son", "sa", "ses", "leur", "leurs", "il", "elle",
        "ils", "elles", "nous", "vous", "être", "sont", "été", "avec", "plus", "ou", "ainsi", "lorsque",
    ];

    // Texte d'interface d'un site, jamais dans un texte juridique : bandeaux, menus, liens de navigation.
    // Volontairement précis : « cookie » seul figure légitimement dans les fiches de la CNIL.
    private static readonly string[] InterfaceText =
    [
        "accepter les cookies", "gérer les cookies", "paramétrer les cookies", "tout accepter", "tout refuser",
        "aller au contenu", "retour en haut", "partager sur", "se connecter", "menu principal",
        "activez javascript", "javascript est désactivé",
    ];

    public static QualityReport Validate(QualityInput input, QualityOptions options) =>
        new([
            TextLength(input, options),
            FrenchLanguage(input, options),
            PassageCount(input, options),
            PassageLength(input, options),
            Duplicates(input, options),
            Structure(input, options),
            HtmlResidue(input),
            Interface(input),
        ]);

    private static QualityCheck TextLength(QualityInput input, QualityOptions options)
    {
        var length = input.Blocks.Sum(b => b.Text.Length);
        return new("texte-minimal", length >= options.MinTextLength, length, options.MinTextLength,
            $"au moins {QualityReport.Format(options.MinTextLength)} caractères de texte extrait");
    }

    private static QualityCheck FrenchLanguage(QualityInput input, QualityOptions options)
    {
        var words = Words().Matches(string.Join(' ', input.Blocks.Select(b => b.Text)).ToLowerInvariant())
            .Select(m => m.Value[(m.Value.LastIndexOfAny(['\'', '’']) + 1)..]) // « l'article » → « article »
            .ToList();
        var share = words.Count == 0 ? 0 : Math.Round((double)words.Count(FrenchFunctionWords.Contains) / words.Count, 3);
        return new("langue-francaise", share >= options.MinFrenchShare, share, options.MinFrenchShare,
            $"au moins {QualityReport.Format(options.MinFrenchShare)} de mots outils français (le, de, et...)");
    }

    private static QualityCheck PassageCount(QualityInput input, QualityOptions options)
    {
        var count = input.Passages.Count;
        if (input.PublishedPassageCount is not { } published || published == 0)
            return new("nombre-de-passages", count > 0, count, 1, "au moins un passage", "sans version publiée à comparer");

        var change = Math.Round(Math.Abs(count - published) / (double)published, 3);
        return new("nombre-de-passages", change <= options.MaxPassageCountChange, change, options.MaxPassageCountChange,
            $"écart d'au plus {QualityReport.Format(options.MaxPassageCountChange)} avec la version publiée",
            $"{count} passages, contre {published} dans la version publiée");
    }

    private static QualityCheck PassageLength(QualityInput input, QualityOptions options)
    {
        var abnormal = input.Passages.Where(p => p.Content.Length < options.MinPassageLength || p.Content.Length > options.MaxPassageLength).ToList();
        var share = Share(abnormal.Count, input.Passages.Count);
        return new("longueur-des-passages", share <= options.MaxAbnormalShare, share, options.MaxAbnormalShare,
            $"au plus {QualityReport.Format(options.MaxAbnormalShare)} de passages de moins de {options.MinPassageLength} ou plus de {options.MaxPassageLength} caractères",
            $"{abnormal.Count} sur {input.Passages.Count}");
    }

    private static QualityCheck Duplicates(QualityInput input, QualityOptions options)
    {
        var duplicated = input.Passages.GroupBy(p => p.Content).Where(g => g.Count() > 1).ToList();
        var share = Share(duplicated.Sum(g => g.Count() - 1), input.Passages.Count);
        return new("doublons", share <= options.MaxDuplicateShare, share, options.MaxDuplicateShare,
            $"au plus {QualityReport.Format(options.MaxDuplicateShare)} de passages en double",
            duplicated.Count == 0 ? null : $"ex. « {Excerpt(duplicated[0].Key)} »");
    }

    private static QualityCheck Structure(QualityInput input, QualityOptions options) => input.SourceSlug switch
    {
        "eur-lex" => Minimum("structure", input.Passages.Select(p => p.Anchor).Where(a => a?.StartsWith("art_") == true).Distinct().Count(),
            options.MinArticles, $"au moins {options.MinArticles} articles distincts"),
        "cnil" => Minimum("structure", input.Blocks.Count(b => b.Kind == BlockKind.Heading && b.Level >= 2),
            options.MinSections, $"au moins {options.MinSections} intertitres"),
        _ => new("structure", true, 0, 0, "aucune règle pour cette source", "sans objet"),
    };

    private static QualityCheck HtmlResidue(QualityInput input)
    {
        var found = input.Passages.FirstOrDefault(p => HtmlTagOrEntity().IsMatch(p.Content));
        var count = input.Passages.Count(p => HtmlTagOrEntity().IsMatch(p.Content));
        return new("residus-html", count == 0, count, 0, "aucun passage avec balise ou entité HTML",
            found is null ? null : $"ex. « {Excerpt(HtmlTagOrEntity().Match(found.Content).Value)} »");
    }

    private static QualityCheck Interface(QualityInput input)
    {
        var hits = input.Passages
            .Select(p => InterfaceText.FirstOrDefault(t => p.Content.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .Where(t => t is not null).ToList();
        return new("texte-d-interface", hits.Count == 0, hits.Count, 0, "aucun passage avec du texte d'interface (menus, cookies...)",
            hits.Count == 0 ? null : $"ex. « {hits[0]} »");
    }

    private static QualityCheck Minimum(string name, int value, int minimum, string rule) =>
        new(name, value >= minimum, value, minimum, rule);

    private static double Share(int part, int total) => total == 0 ? 0 : Math.Round((double)part / total, 3);

    private static string Excerpt(string text) => text.Length <= 60 ? text : text[..59] + "…";

    [GeneratedRegex(@"[\p{L}'’]+")]
    private static partial Regex Words();

    // Une vraie balise n'a pas d'espace après « < » : « x < y et z > w » n'en est pas une.
    [GeneratedRegex(@"</?[a-zA-Z][a-zA-Z0-9]*(\s[^<>]*)?/?>|&(nbsp|amp|lt|gt|quot|apos|#\d+|#x[0-9a-fA-F]+);")]
    private static partial Regex HtmlTagOrEntity();
}

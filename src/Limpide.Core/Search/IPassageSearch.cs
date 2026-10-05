namespace Limpide.Core.Search;

/// <summary>
/// Recherche des passages les plus proches d'une question, parmi les versions courantes des documents.
/// Utilisée par la console (search, evaluate) et par la génération des réponses.
/// </summary>
public interface IPassageSearch
{
    /// <summary>Nom de la stratégie (ex. « vectorielle (bge-m3+titre) ») : affiché dans le panneau « sous le capot ».</summary>
    string Strategy { get; }

    /// <summary>Passages triés du plus proche au moins proche, avec la durée de chaque étape.</summary>
    Task<SearchOutcome> SearchAsync(string question, int limit, CancellationToken ct);
}

/// <param name="EmbeddingDuration">Vectorisation de la question.</param>
/// <param name="QueryDuration">Requête dans la base.</param>
public sealed record SearchOutcome(
    IReadOnlyList<RetrievedPassage> Passages,
    TimeSpan EmbeddingDuration,
    TimeSpan QueryDuration);

/// <summary>
/// Un passage trouvé, avec ce qu'il faut pour le citer : source, licence, URL et date de collecte de la version utilisée.
/// </summary>
/// <param name="Content">Texte de la source, reproduit sans modification.</param>
/// <param name="Score">Similarité cosinus entre la question et le passage (1 = identique).</param>
/// <param name="ReuseTerms">Conditions de réutilisation de la source, à afficher avec l'extrait.</param>
public sealed record RetrievedPassage(
    string Heading,
    string? Anchor,
    string Content,
    double Score,
    string SourceName,
    string DocumentTitle,
    string Url,
    DateTimeOffset FetchedAt,
    string ReuseTerms);

namespace Limpide.Core.Search;

/// <summary>
/// Recherche des passages les plus proches d'une question, parmi les versions courantes des documents.
/// Utilisée par la console (search, evaluate) et, en semaine 2, par la génération des réponses.
/// </summary>
public interface IPassageSearch
{
    /// <summary>Nom de la stratégie (ex. « vectorielle, bge-m3+titre ») : affiché dans le panneau « sous le capot ».</summary>
    string Strategy { get; }

    /// <summary>Passages triés du plus proche au moins proche.</summary>
    Task<IReadOnlyList<RetrievedPassage>> SearchAsync(string question, int limit, CancellationToken ct);
}

/// <summary>
/// Un passage trouvé, avec ce qu'il faut pour le citer : source, URL et date de collecte de la version utilisée.
/// </summary>
/// <param name="Score">Similarité cosinus entre la question et le passage (1 = identique).</param>
public sealed record RetrievedPassage(
    string Heading,
    string? Anchor,
    string Content,
    double Score,
    string SourceName,
    string DocumentTitle,
    string Url,
    DateTimeOffset FetchedAt);

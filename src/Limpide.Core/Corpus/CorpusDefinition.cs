namespace Limpide.Core.Corpus;

/// <summary>Liste des documents à collecter, lue depuis <c>corpus.json</c>.</summary>
public sealed record CorpusDefinition(IReadOnlyList<SourceDefinition> Sources);

/// <summary>Un producteur de documents (EUR-Lex, CNIL...) et ses conditions de réutilisation.</summary>
public sealed record SourceDefinition(
    string Name,
    string BaseUrl,
    string ReuseTerms,
    IReadOnlyList<DocumentDefinition> Documents)
{
    /// <summary>Nom de dossier sous <c>data/raw</c> (ex. « EUR-Lex » → « eur-lex »).</summary>
    public string Slug => Slugs.From(Name);
}

/// <summary>
/// Un document du corpus. <see cref="Url"/> identifie le document et sert aux citations ;
/// <see cref="FetchUrl"/> permet de le télécharger ailleurs quand la page publique n'est pas
/// accessible à un script (ex. EUR-Lex, protégé par un défi JavaScript).
/// </summary>
public sealed record DocumentDefinition(
    string Title,
    string Url,
    DocumentFormat Format,
    string? FetchUrl = null,
    string? Accept = null,
    string? AcceptLanguage = null)
{
    public string EffectiveFetchUrl => FetchUrl ?? Url;
}

public enum DocumentFormat
{
    Html,
    Pdf,
}

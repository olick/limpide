using System.Text.Json;
using System.Text.Json.Serialization;

namespace Limpide.Core.Corpus;

public static class CorpusLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
    };

    public static CorpusDefinition Load(string path) => Parse(File.ReadAllText(path));

    /// <summary>Lit et valide la liste du corpus. Lève <see cref="InvalidDataException"/> si elle est incohérente.</summary>
    public static CorpusDefinition Parse(string json)
    {
        CorpusDefinition? corpus;
        try
        {
            corpus = JsonSerializer.Deserialize<CorpusDefinition>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"corpus.json illisible : {ex.Message}", ex);
        }

        if (corpus is null)
            throw new InvalidDataException("corpus.json est vide.");

        Validate(corpus);
        return corpus;
    }

    private static void Validate(CorpusDefinition corpus)
    {
        var errors = new List<string>();

        if (corpus.Sources.Count == 0)
            errors.Add("aucune source déclarée");

        foreach (var name in Duplicates(corpus.Sources.Select(s => s.Slug)))
            errors.Add($"deux sources ont le même nom de dossier « {name} »");

        foreach (var source in corpus.Sources)
        {
            if (string.IsNullOrWhiteSpace(source.Name) || source.Slug.Length == 0)
                errors.Add($"source sans nom exploitable : « {source.Name} »");
            if (string.IsNullOrWhiteSpace(source.ReuseTerms))
                errors.Add($"{source.Name} : conditions de réutilisation manquantes (voir docs/sources.md)");
            if (!IsHttpUrl(source.BaseUrl))
                errors.Add($"{source.Name} : baseUrl invalide « {source.BaseUrl} »");

            foreach (var document in source.Documents)
            {
                if (string.IsNullOrWhiteSpace(document.Title))
                    errors.Add($"{source.Name} : document sans titre ({document.Url})");
                if (!IsHttpUrl(document.Url))
                    errors.Add($"{source.Name} : url invalide « {document.Url} »");
                if (document.FetchUrl is not null && !IsHttpUrl(document.FetchUrl))
                    errors.Add($"{source.Name} : fetchUrl invalide « {document.FetchUrl} »");
            }
        }

        foreach (var url in Duplicates(corpus.Sources.SelectMany(s => s.Documents).Select(d => d.Url)))
            errors.Add($"document déclaré deux fois : {url}");

        if (errors.Count > 0)
            throw new InvalidDataException("corpus.json invalide :\n- " + string.Join("\n- ", errors));
    }

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    private static IEnumerable<string> Duplicates(IEnumerable<string> values) =>
        values.GroupBy(v => v, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key);
}

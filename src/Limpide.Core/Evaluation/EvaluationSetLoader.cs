using System.Text.Json;

namespace Limpide.Core.Evaluation;

public static class EvaluationSetLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
    };

    public static EvaluationSet Load(string path) => Parse(File.ReadAllText(path));

    public static EvaluationSet Parse(string json)
    {
        EvaluationSet? set;
        try
        {
            set = JsonSerializer.Deserialize<EvaluationSet>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"jeu de questions illisible : {ex.Message}", ex);
        }

        if (set is null || set.Questions.Count == 0)
            throw new InvalidDataException("jeu de questions vide");

        var errors = new List<string>();
        foreach (
            var id in set.Questions.GroupBy(q => q.Id).Where(g => g.Count() > 1).Select(g => g.Key)
        )
            errors.Add($"identifiant en double : {id}");
        foreach (
            var q in set.Questions.Where(q =>
                (q.Expected.Anchors?.Count ?? 0) + (q.Expected.Headings?.Count ?? 0) == 0
            )
        )
            errors.Add($"{q.Id} : aucun passage attendu");

        if (errors.Count > 0)
            throw new InvalidDataException(
                "jeu de questions invalide :\n- " + string.Join("\n- ", errors)
            );

        return set;
    }
}

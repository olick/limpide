using System.Globalization;
using System.Text.Json;

namespace Limpide.Web;

/// <summary>
/// Une évaluation publiée : un rapport JSON de <c>evaluate-answers</c> versionné dans <c>eval/results/</c>.
/// Les métriques ajoutées à l'outil au fil des versions manquent dans les rapports anciens : null, affiché « — ».
/// </summary>
public sealed record EvaluationRun(
    string Name,
    DateTimeOffset Date,
    string Prompt,
    string Model,
    string Embedding,
    string QuestionSet,
    int Questions,
    int? Ok,
    int? Failed,
    int? ToReview,
    int? AnswerQuestions,
    int? ExpectedPassageCited,
    int? DeclineQuestions,
    int? CorrectDeclines,
    int? InjectionQuestions,
    int? InjectionsFailed,
    int? Judged,
    int? Unfaithful,
    double? CostPerQuestion,
    string CostCurrency,
    double? MedianSeconds,
    double? P95Seconds,
    double? PassCost,
    double? JudgeCost,
    IReadOnlyList<EvaluationAnswer> Answers);

/// <summary>Une question du jeu dans un rapport : comportement attendu, verdict, garde-fous, verdict du juge.</summary>
/// <param name="Behavior">Answer, Decline, NoLegalQualification ou Resist (eval/questions.json).</param>
/// <param name="Status">Ok, Failed ou ToReview.</param>
/// <param name="Judge">« fidèle », « infidèle », ou null quand le juge n'a pas relu la réponse (refus).</param>
public sealed record EvaluationAnswer(string Id, string Behavior, string Status, IReadOnlyList<string> Guardrails, string? Judge)
{
    public bool Has(string guardrail) => Guardrails.Contains(guardrail);
}

/// <summary>
/// Rapports d'évaluation copiés à côté de l'application (dossier <c>evaluation</c>, voir Limpide.Web.csproj), lus une
/// fois au démarrage. Les rapports de la chaîne (artefacts d'exécution) n'y sont pas : seuls ceux du dépôt.
/// </summary>
public sealed class EvaluationReports
{
    public IReadOnlyList<EvaluationRun> Runs { get; }

    public EvaluationReports(ILogger<EvaluationReports> logger)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "evaluation");
        var runs = new List<EvaluationRun>();
        if (Directory.Exists(directory))
        {
            foreach (var file in Directory.GetFiles(directory, "*.json"))
            {
                try
                {
                    using var json = JsonDocument.Parse(File.ReadAllText(file));
                    runs.Add(Read(Path.GetFileNameWithoutExtension(file), json.RootElement));
                }
                catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
                {
                    logger.LogWarning(e, "Rapport d'évaluation illisible, ignoré : {File}", Path.GetFileName(file));
                }
            }
        }
        Runs = runs.OrderByDescending(r => r.Date).ToList();
    }

    /// <summary>La plus récente évaluation de la configuration en ligne (consignes, modèle, vectorisation), ou null.</summary>
    public EvaluationRun? Latest(string prompt, string model, string embedding) =>
        Runs.FirstOrDefault(r => IsServed(r, prompt, model, embedding));

    public static bool IsServed(EvaluationRun run, string prompt, string model, string embedding) =>
        run.Prompt == prompt && run.Model == model && run.Embedding == embedding;

    private static EvaluationRun Read(string name, JsonElement report)
    {
        var versions = report.GetProperty("versions");
        var metrics = report.GetProperty("metrics");
        var judge = metrics.TryGetProperty("judge", out var j) ? j : default;
        return new EvaluationRun(
            name,
            DateTimeOffset.Parse(report.GetProperty("date").GetString()!, CultureInfo.InvariantCulture),
            versions.GetProperty("prompt").GetString()!,
            versions.GetProperty("model").GetString()!,
            versions.GetProperty("embedding").GetString()!,
            versions.GetProperty("questionSet").GetProperty("sha256").GetString()!,
            metrics.GetProperty("questions").GetInt32(),
            Int(metrics, "ok"),
            Int(metrics, "failed"),
            Int(metrics, "toReview"),
            Int(metrics, "answerQuestions"),
            Int(metrics, "expectedPassageCited"),
            Int(metrics, "declineQuestions"),
            Int(metrics, "correctDeclines"),
            Int(metrics, "injectionQuestions"),
            Int(metrics, "injectionsFailed"),
            Int(judge, "judged"),
            Int(judge, "unfaithful"),
            // En euros depuis la S8 ; les rapports antérieurs, gardés tels quels, sont en dollars.
            Number(metrics, "costPerQuestionEur") ?? Number(metrics, "costPerQuestionUsd"),
            Number(metrics, "costPerQuestionEur") is null ? "$" : "€",
            metrics.TryGetProperty("latencySeconds", out var latency) ? Number(latency, "median") : null,
            latency.ValueKind == JsonValueKind.Object ? Number(latency, "p95") : null,
            // Coût d'une passe : rédaction et juge, en euros seulement (rapports depuis la S8).
            Number(metrics, "costEur") is { } drafting ? drafting + (Number(judge, "costEur") ?? 0) : null,
            Number(judge, "costEur"),
            report.TryGetProperty("questions", out var questions) && questions.ValueKind == JsonValueKind.Array
                ? questions.EnumerateArray().Select(ReadAnswer).ToList()
                : []);
    }

    private static EvaluationAnswer ReadAnswer(JsonElement q) => new(
        q.GetProperty("id").GetString()!,
        q.GetProperty("behavior").GetString()!,
        q.GetProperty("status").GetString()!,
        q.TryGetProperty("guardrails", out var g) && g.ValueKind == JsonValueKind.Array
            ? g.EnumerateArray().Select(x => x.GetString()!).ToList()
            : [],
        q.TryGetProperty("judge", out var j) && j.ValueKind == JsonValueKind.Object && j.TryGetProperty("verdict", out var v)
            ? v.GetString()
            : null);

    private static int? Int(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetInt32() : null;

    private static double? Number(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetDouble() : null;
}

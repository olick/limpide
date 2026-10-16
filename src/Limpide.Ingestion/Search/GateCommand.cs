using System.Text;
using System.Text.Json;
using Limpide.Core.Evaluation;

namespace Limpide.Ingestion.Search;

/// <summary>Seuils de la section « EvaluationGate » (appsettings de la console), justifiés dans l'ADR-014.</summary>
public sealed class EvaluationGateOptions
{
    public List<GateRule> Rules { get; set; } = [];
}

/// <summary>
/// Barrière de qualité (S7) : lit un rapport JSON d'evaluate-answers (le plus récent par défaut) et le compare aux
/// seuils. Code de sortie 1 si un seuil bloquant échoue : c'est ce code qui arrête la CI. Écrit aussi le tableau en
/// Markdown dans le fichier désigné par GITHUB_STEP_SUMMARY, s'il existe.
/// </summary>
public sealed class GateCommand(EvaluationGateOptions gate, IngestionOptions options)
{
    public async Task<int> RunAsync(string? reportPath, CancellationToken ct)
    {
        if (gate.Rules.Count == 0)
            throw new InvalidOperationException("Aucun seuil configuré (section EvaluationGate) : la barrière laisserait tout passer.");

        var path = reportPath is { Length: > 0 } ? reportPath : Latest();
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(path, ct));
        var results = EvaluationGate.Check(report.RootElement, gate.Rules);
        var blocks = EvaluationGate.Blocks(results);

        var table = Table(path, report.RootElement, results, blocks);
        Console.WriteLine(table);
        if (Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY") is { Length: > 0 } summary)
            await File.AppendAllTextAsync(summary, table + "\n", ct);
        return blocks ? 1 : 0;
    }

    private string Latest() =>
        Directory.EnumerateFiles(options.ResultsPath, "*.json").Order().LastOrDefault()
        ?? throw new InvalidOperationException($"Aucun rapport JSON dans {options.ResultsPath} : lancer evaluate-answers d'abord.");

    private static string Table(string path, JsonElement report, IReadOnlyList<GateResult> results, bool blocks)
    {
        string Version(string name) => report.TryGetProperty("versions", out var v) && v.TryGetProperty(name, out var x)
            ? x.ValueKind == JsonValueKind.String ? x.GetString()! : x.ToString()
            : "?";

        var text = new StringBuilder()
            .AppendLine($"## Barrière de qualité : {(blocks ? "❌ BLOQUÉ" : "✅ passe")}")
            .AppendLine()
            .AppendLine($"Rapport `{Path.GetFileName(path)}` — consignes `{Version("prompt")}`, modèle `{Version("model")}`, "
                + $"vectorisation `{Version("embedding")}`")
            .AppendLine()
            .AppendLine("| | Métrique | Valeur | Seuil | Bloquant |")
            .AppendLine("|---|---|---|---|---|");
        foreach (var r in results)
        {
            var mark = r.Status switch { GateStatus.Passed => "✅", GateStatus.Warning => "⚠️", _ => "❌" };
            var value = r.Value is { } v ? EvaluationGate.Number(v) : "absente";
            text.AppendLine($"| {mark} | `{r.Rule.Metric}` | {value} | {EvaluationGate.Describe(r.Rule)} | {(r.Rule.Blocking ? "oui" : "non")} |");
        }

        var failures = results.Where(r => r.Status != GateStatus.Passed).ToList();
        if (failures.Count > 0)
        {
            text.AppendLine();
            foreach (var r in failures)
                text.AppendLine($"- `{r.Rule.Metric}` : {r.Rule.Why}");
        }
        return text.ToString();
    }
}

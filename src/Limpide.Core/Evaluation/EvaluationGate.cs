using System.Globalization;
using System.Text.Json;

namespace Limpide.Core.Evaluation;

/// <summary>
/// Un seuil sur une métrique du rapport JSON d'evaluate-answers (S7). Bloquant : la mise en production s'arrête ;
/// sinon : avertissement seulement (métrique trop variable pour décider seule, comme la latence d'une API externe).
/// </summary>
/// <param name="Metric">Chemin dans le rapport, propriétés séparées par des points : « metrics.promptLeaks ».</param>
/// <param name="Why">Ce que le seuil protège, et d'où vient sa valeur (affiché quand il échoue).</param>
public sealed record GateRule(string Metric, double? Min = null, double? Max = null, bool Blocking = true, string? Why = null);

public enum GateStatus
{
    Passed,

    /// <summary>Seuil non bloquant dépassé.</summary>
    Warning,

    /// <summary>Seuil bloquant dépassé, ou métrique absente du rapport (un seuil qu'on ne peut pas lire ne passe pas).</summary>
    Failed,
}

public sealed record GateResult(GateRule Rule, double? Value, GateStatus Status);

/// <summary>
/// Compare un rapport d'évaluation aux seuils. Sans E/S : le rapport arrive déjà lu, les seuils viennent de la
/// configuration.
/// </summary>
public static class EvaluationGate
{
    public static IReadOnlyList<GateResult> Check(JsonElement report, IEnumerable<GateRule> rules) =>
        rules.Select(rule =>
        {
            var value = Read(report, rule.Metric);
            var ok = value is { } v && (rule.Min is not { } min || v >= min) && (rule.Max is not { } max || v <= max);
            var status = ok ? GateStatus.Passed : rule.Blocking || value is null ? GateStatus.Failed : GateStatus.Warning;
            return new GateResult(rule, value, status);
        }).ToList();

    public static bool Blocks(IEnumerable<GateResult> results) => results.Any(r => r.Status == GateStatus.Failed);

    /// <summary>Lit un nombre au chemin donné, sans tenir compte de la casse ; null s'il manque ou n'est pas un nombre.</summary>
    public static double? Read(JsonElement report, string path)
    {
        var current = report;
        foreach (var name in path.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object)
                return null;
            var match = current.EnumerateObject().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (match.Value.ValueKind == JsonValueKind.Undefined)
                return null;
            current = match.Value;
        }
        return current.ValueKind == JsonValueKind.Number ? current.GetDouble() : null;
    }

    public static string Describe(GateRule rule) => (rule.Min, rule.Max) switch
    {
        ({ } min, { } max) => $"entre {Number(min)} et {Number(max)}",
        ({ } min, null) => $"≥ {Number(min)}",
        (null, { } max) => $"≤ {Number(max)}",
        _ => "présente",
    };

    public static string Number(double value) => value.ToString("0.####", CultureInfo.GetCultureInfo("fr-FR"));
}

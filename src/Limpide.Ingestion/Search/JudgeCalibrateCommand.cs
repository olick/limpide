using System.Globalization;
using System.Text;
using System.Text.Json;
using Limpide.Core.Evaluation;

namespace Limpide.Ingestion.Search;

/// <summary>
/// Mesure la justesse du juge de fidélité sur des réponses déjà relues (eval/calibration/fidelite.json) : combien
/// d'erreurs connues il détecte, combien de fausses alertes il donne sur des réponses fidèles. Sans cette mesure,
/// son verdict ne vaut rien comme critère de blocage (S7, session 3).
/// </summary>
public sealed class JudgeCalibrateCommand(FaithfulnessJudge judge, IngestionOptions options)
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private sealed record CalibrationSet(List<CalibrationCase> Cases);

    private sealed record CalibrationCase(string Id, string Question, string Answer, List<JudgedPassage> Passages, string Reference, string Review);

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var set = JsonSerializer.Deserialize<CalibrationSet>(
            await File.ReadAllTextAsync(options.JudgeCalibrationPath, ct), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var rows = new List<(CalibrationCase Case, FaithfulnessJudgment Judgment)>();

        Console.WriteLine($"Calibration du juge : {options.JudgeCalibrationPath} — {set.Cases.Count} réponses relues\n");
        foreach (var c in set.Cases)
        {
            var judgment = await JudgeWithRetry(judge, c.Question, c.Answer, c.Passages, ct);
            rows.Add((c, judgment));
            var agrees = Agrees(c.Reference, judgment.Verdict) ? "  " : "≠ ";
            Console.WriteLine($"{agrees}{c.Id,-45} relecture : {c.Reference,-8} juge : {Label(judgment.Verdict),-9} {judgment.Duration.TotalSeconds.ToString("0.0", French)} s");
        }

        var summary = Summary(rows);
        Console.WriteLine($"\n{summary}");

        var model = rows[0].Judgment.Model;
        var path = Path.Combine(options.ResultsPath, $"{DateTime.Now:yyyy-MM-dd-HHmm}-juge-{model}.md");
        Directory.CreateDirectory(options.ResultsPath);
        await File.WriteAllTextAsync(path, Report(rows, summary), ct);
        Console.WriteLine($"Rapport : {path}");
        return 0;
    }

    /// <summary>L'API limite le débit (429 vu à la 37e question d'affilée) : trois essais, 10 puis 20 secondes d'attente.</summary>
    internal static async Task<FaithfulnessJudgment> JudgeWithRetry(
        FaithfulnessJudge judge, string question, string answer, IReadOnlyList<JudgedPassage> passages, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await judge.JudgeAsync(question, answer, passages, ct);
            }
            catch (System.ClientModel.ClientResultException e) when (e.Status == 429 && attempt < 3)
            {
                await Task.Delay(TimeSpan.FromSeconds(10 * attempt), ct);
            }
        }
    }

    /// <summary>
    /// Accord sur ce qui compte pour bloquer : une réponse infidèle doit être signalée, une réponse fidèle ne doit pas
    /// l'être. Une réserve de la relecture s'accorde avec « fidèle » comme avec « réserve » du juge.
    /// </summary>
    private static bool Agrees(string reference, FaithfulnessVerdict verdict) => (reference, verdict) switch
    {
        ("infidele", FaithfulnessVerdict.Unfaithful) => true,
        ("fidele" or "reserve", FaithfulnessVerdict.Faithful or FaithfulnessVerdict.Reservation) => true,
        _ => false,
    };

    private static string Summary(List<(CalibrationCase Case, FaithfulnessJudgment Judgment)> rows)
    {
        int Count(string reference, FaithfulnessVerdict verdict) => rows.Count(r => r.Case.Reference == reference && r.Judgment.Verdict == verdict);
        int Total(string reference) => rows.Count(r => r.Case.Reference == reference);
        var verdicts = new[] { FaithfulnessVerdict.Faithful, FaithfulnessVerdict.Reservation, FaithfulnessVerdict.Unfaithful, FaithfulnessVerdict.Unreadable };

        var text = new StringBuilder();
        text.AppendLine($"Erreurs connues détectées (infidèle) : {Count("infidele", FaithfulnessVerdict.Unfaithful)}/{Total("infidele")}");
        text.AppendLine($"Fausses alertes sur des réponses fidèles : {Count("fidele", FaithfulnessVerdict.Unfaithful)}/{Total("fidele")}");
        text.AppendLine($"Réponses à réserves jugées infidèles : {Count("reserve", FaithfulnessVerdict.Unfaithful)}/{Total("reserve")}");
        text.AppendLine($"Accord sur le critère de blocage : {rows.Count(r => Agrees(r.Case.Reference, r.Judgment.Verdict))}/{rows.Count}");
        text.AppendLine();
        text.AppendLine("| Relecture \\ juge | fidèle | réserve | infidèle | illisible |");
        text.AppendLine("|---|---|---|---|---|");
        foreach (var reference in new[] { "fidele", "reserve", "infidele" })
            text.AppendLine($"| {reference} | {string.Join(" | ", verdicts.Select(v => Count(reference, v)))} |");
        text.AppendLine();

        var costs = rows.Select(r => r.Judgment.Usage.EstimatedCost).ToList();
        var durations = rows.Select(r => r.Judgment.Duration).Order().ToList();
        text.Append($"Coût : {(costs.Any(c => c is null) ? "inconnu" : $"{costs.Sum()!.Value.ToString("0.0000", French)} €")}"
            + $" · durée médiane {durations[durations.Count / 2].TotalSeconds.ToString("0.0", French)} s, max {durations[^1].TotalSeconds.ToString("0.0", French)} s");
        return text.ToString();
    }

    private static string Report(List<(CalibrationCase Case, FaithfulnessJudgment Judgment)> rows, string summary)
    {
        var first = rows[0].Judgment;
        var text = new StringBuilder();
        text.AppendLine($"# Calibration du juge de fidélité — {DateTime.Now:yyyy-MM-dd HH:mm}");
        text.AppendLine();
        text.AppendLine($"Juge : `{first.Model}`, consignes `{first.PromptVersion}`. Référence : relectures écrites dans les rapports");
        text.AppendLine("(par Claude, pas par un juriste). Jeu : `eval/calibration/fidelite.json`.");
        text.AppendLine();
        text.AppendLine(summary);
        text.AppendLine();
        text.AppendLine("## Désaccords");
        foreach (var (c, j) in rows.Where(r => !Agrees(r.Case.Reference, r.Judgment.Verdict)))
            Case(c, j);
        text.AppendLine();
        text.AppendLine("## Accords");
        foreach (var (c, j) in rows.Where(r => Agrees(r.Case.Reference, r.Judgment.Verdict)))
            Case(c, j);
        return text.ToString();

        void Case(CalibrationCase c, FaithfulnessJudgment j)
        {
            text.AppendLine();
            text.AppendLine($"### {c.Id} — {c.Question}");
            text.AppendLine();
            text.AppendLine($"- Relecture : **{c.Reference}** — {c.Review}");
            text.AppendLine($"- Juge : **{Label(j.Verdict)}**");
            foreach (var issue in j.Issues)
                text.AppendLine($"  - `{issue.Kind}` « {issue.Excerpt} » — {issue.Explanation}");
        }
    }

    private static string Label(FaithfulnessVerdict verdict) => verdict switch
    {
        FaithfulnessVerdict.Faithful => "fidèle",
        FaithfulnessVerdict.Reservation => "réserve",
        FaithfulnessVerdict.Unfaithful => "infidèle",
        _ => "illisible",
    };
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Limpide.Core.Answering;
using Limpide.Core.Evaluation;
using Limpide.Infrastructure;
using Limpide.Infrastructure.Storage;

namespace Limpide.Ingestion.Search;

/// <summary>
/// Pose tout le jeu de questions à la chaîne complète (recherche, seuil, modèle, contrôles), vérifie ce qui peut
/// l'être automatiquement, et écrit un rapport Markdown dans eval/results/ avec le texte de chaque réponse,
/// pour la relecture (fidélité au texte, absence de qualification juridique), et un rapport JSON lisible par la CI
/// (S7) : versions de tout ce qui influence la qualité, métriques, détail par question.
/// </summary>
public sealed class EvaluateAnswersCommand(
    AnswerService service, GenerationSettings generation, IngestionOptions options, EmbeddingOptions embedding, ChunkStore chunks,
    FaithfulnessJudge judge)
{
    /// <summary>Verdict du juge de fidélité par question ; absent quand il n'y a rien à juger (refus, aucune citation).</summary>
    private readonly Dictionary<string, FaithfulnessJudgment> judgments = [];

    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var set = EvaluationSetLoader.Load(options.QuestionsPath);
        var checks = new List<AnswerCheck>();

        Console.WriteLine($"Jeu : {options.QuestionsPath} — modèle : {generation.Model} — {set.Questions.Count} questions\n");
        foreach (var question in set.Questions)
        {
            var result = await service.AskAsync(question.Question, ct);
            var check = AnswerChecks.Check(question, result);
            checks.Add(check);

            var judged = "";
            if (options.JudgeAnswers && !result.Declined && result.CitedPassages.Any())
            {
                var judgment = await JudgeCalibrateCommand.JudgeWithRetry(judge, question.Question, result.Answer,
                    result.CitedPassages.Select(p => new JudgedPassage(p.Id, p.Passage.Heading, p.Passage.Content)).ToList(), ct);
                judgments[question.Id] = judgment;
                judged = $" · juge : {JudgeLabel(judgment.Verdict)}";
            }
            Console.WriteLine($"{question.Id,-4} {Label(check.Status),-9} {Seconds(result.Timings.Total),7} {Cost(result.Usage.EstimatedCost),11}  {check.Verdict}{judged}");
        }

        var summary = Summary(checks);
        Console.WriteLine($"\n{summary}");

        // L'heure dans le nom : deux passes du même jour ne s'écrasent pas (les réponses varient d'une passe à l'autre).
        var path = Path.Combine(options.ResultsPath, $"{DateTime.Now:yyyy-MM-dd-HHmm}-{generation.Model}.md");
        Directory.CreateDirectory(options.ResultsPath);
        await File.WriteAllTextAsync(path, Report(checks, summary), ct);
        var json = Path.ChangeExtension(path, ".json");
        await File.WriteAllTextAsync(json, JsonSerializer.Serialize(await JsonReport(checks, ct), Json), ct);
        Console.WriteLine($"Rapport (réponses complètes, à relire) : {path}");
        Console.WriteLine($"Rapport JSON (CI) : {json}");
        return 0;
    }

    private static double? Share(int count, int total) => total == 0 ? null : Math.Round((double)count / total, 4);

    private static bool Provided(AnswerCheck check) =>
        check.Question.Expected is { } expected && check.Result.Passages.Any(p => expected.Matches(p.Passage.Anchor, p.Passage.Heading));

    /// <summary>
    /// Ce que la CI compare d'une passe à l'autre. Les versions disent ce qui a changé ; les métriques, l'effet.
    /// </summary>
    private async Task<object> JsonReport(List<AnswerCheck> checks, CancellationToken ct)
    {
        var results = checks.Select(c => c.Result).ToList();
        var totals = results.Select(r => r.Timings.Total).ToList();
        var costs = results.Select(r => r.Usage.EstimatedCost).ToList();
        var answers = checks.Where(c => c.Question.Behavior == ExpectedBehavior.Answer).ToList();
        var declines = checks.Where(c => c.Question.Behavior == ExpectedBehavior.Decline).ToList();
        var injections = checks.Where(c => c.Question.Behavior == ExpectedBehavior.Resist).ToList();
        var corpus = await chunks.FingerprintAsync(ct);
        var questionsFile = await File.ReadAllBytesAsync(options.QuestionsPath, ct);

        return new
        {
            Date = DateTimeOffset.Now,
            Versions = new
            {
                Prompt = results[0].PromptVersion,
                Model = generation.Model,
                Temperature = generation.Temperature,
                Embedding = embedding.Label,
                // Ne s'applique qu'aux questions : absente du libellé des vecteurs, mais change la recherche.
                embedding.QueryInstruction,
                Search = results[0].SearchStrategy,
                generation.PassageCount,
                generation.MinScore,
                QuestionSet = new
                {
                    Path = options.QuestionsPath,
                    Questions = checks.Count,
                    Sha256 = Convert.ToHexStringLower(SHA256.HashData(questionsFile))[..12],
                },
                Corpus = corpus,
                Judge = options.JudgeAnswers ? new { judge.Model, Prompt = judge.PromptVersion } : null,
            },
            Metrics = new
            {
                Questions = checks.Count,
                Ok = checks.Count(c => c.Status == CheckStatus.Ok),
                Failed = checks.Count(c => c.Status == CheckStatus.Failed),
                ToReview = checks.Count(c => c.Status == CheckStatus.ToReview),
                ExpectedPassageCited = answers.Count(c => c.CitesExpected == true),
                // La recherche, mesurée dans la même passe : le passage attendu figure-t-il parmi ceux fournis au modèle ?
                ExpectedPassageProvided = answers.Count(Provided),
                AnswerQuestions = answers.Count,
                // Proportions : le jeu grandira (retours des visiteurs) ; les seuils du gate ne doivent pas en dépendre.
                ExpectedPassageCitedShare = Share(answers.Count(c => c.CitesExpected == true), answers.Count),
                ExpectedPassageProvidedShare = Share(answers.Count(Provided), answers.Count),
                CorrectDeclineShare = Share(declines.Count(c => c.Status == CheckStatus.Ok), declines.Count),
                CorrectDeclines = declines.Count(c => c.Status == CheckStatus.Ok),
                DeclineQuestions = declines.Count,
                InjectionsFailed = injections.Count(c => c.Status == CheckStatus.Failed),
                InjectionQuestions = injections.Count,
                PromptLeaks = checks.Count(c => c.Result.Guardrails.Any(g => g.Code == "consigne-divulguee")),
                Guardrails = results.SelectMany(r => r.Guardrails).GroupBy(g => g.Code).ToDictionary(g => g.Key, g => g.Count()),
                LatencySeconds = new { Median = Percentile(totals, 0.5).TotalSeconds, P95 = Percentile(totals, 0.95).TotalSeconds },
                // Par étape, sur les questions où elle a eu lieu : une passe lente se rattache à un fournisseur
                // (vectorisation : OVHcloud ; rédaction et juge : Mistral). Constat du 2026-10-28 : 9 min au lieu de 3.
                StepSeconds = new
                {
                    Embedding = StepLatency(results.Select(r => r.Timings.Embedding)),
                    Search = StepLatency(results.Select(r => r.Timings.Search)),
                    Generation = StepLatency(results.Select(r => r.Timings.Generation)),
                    Judge = StepLatency(judgments.Values.Select(j => j.Duration)),
                },
                CostEur = costs.Any(c => c is null) ? (decimal?)null : costs.Sum(),
                CostPerQuestionEur = costs.Any(c => c is null) ? (decimal?)null : Math.Round(costs.Sum()!.Value / checks.Count, 6),
                ModelCalls = results.Count(r => r.Timings.Generation > TimeSpan.Zero),
                Judge = new
                {
                    Judged = judgments.Count,
                    Unfaithful = judgments.Count(j => j.Value.Verdict == FaithfulnessVerdict.Unfaithful),
                    UnfaithfulShare = Share(judgments.Count(j => j.Value.Verdict == FaithfulnessVerdict.Unfaithful), judgments.Count),
                    Unreadable = judgments.Count(j => j.Value.Verdict == FaithfulnessVerdict.Unreadable),
                    CostEur = judgments.Values.Any(j => j.Usage.EstimatedCost is null) ? null : (decimal?)judgments.Values.Sum(j => j.Usage.EstimatedCost),
                },
            },
            Questions = checks.Select(c => new
            {
                c.Question.Id,
                Behavior = c.Question.Behavior.ToString(),
                Status = c.Status.ToString(),
                c.Verdict,
                c.CitesExpected,
                Guardrails = c.Result.Guardrails.Select(g => g.Code),
                Cited = c.Result.CitedPassages.Select(p => p.Passage.Anchor ?? p.Passage.Heading),
                Seconds = c.Result.Timings.Total.TotalSeconds,
                Steps = new
                {
                    Embedding = c.Result.Timings.Embedding.TotalSeconds,
                    Search = c.Result.Timings.Search.TotalSeconds,
                    Generation = c.Result.Timings.Generation.TotalSeconds,
                },
                c.Result.Usage.InputTokens,
                c.Result.Usage.OutputTokens,
                CostEur = c.Result.Usage.EstimatedCost,
                Judge = judgments.GetValueOrDefault(c.Question.Id) is { } j
                    ? new { Verdict = JudgeLabel(j.Verdict), j.Issues, Seconds = j.Duration.TotalSeconds }
                    : null,
            }),
        };
    }

    private string Summary(List<AnswerCheck> checks)
    {
        var results = checks.Select(c => c.Result).ToList();
        var costs = results.Select(r => r.Usage.EstimatedCost).ToList();
        var called = results.Where(r => r.Timings.Generation > TimeSpan.Zero).ToList();
        return string.Join("\n",
            $"Vérifié automatiquement : {checks.Count(c => c.Status == CheckStatus.Ok)}/{checks.Count}"
            + $" · échecs : {checks.Count(c => c.Status == CheckStatus.Failed)}"
            + $" · à relire : {checks.Count(c => c.Status == CheckStatus.ToReview)}",
            $"Passage attendu cité : {checks.Count(c => c.CitesExpected == true)}/{checks.Count(c => c.CitesExpected is not null)}",
            $"Coût total : {(costs.Any(c => c is null) ? "inconnu" : Cost(costs.Sum()))}"
            + $" · modèle appelé pour {called.Count}/{results.Count} questions",
            $"Durée totale par question : médiane {Seconds(Percentile(results.Select(r => r.Timings.Total).ToList(), 0.5))},"
            + $" 95e centile {Seconds(Percentile(results.Select(r => r.Timings.Total).ToList(), 0.95))},"
            + $" max {Seconds(results.Max(r => r.Timings.Total))}",
            "Par étape (médiane / 95e centile) : "
            + string.Join(" · ", new[]
            {
                ("vectorisation", results.Select(r => r.Timings.Embedding)),
                ("base", results.Select(r => r.Timings.Search)),
                ("rédaction", results.Select(r => r.Timings.Generation)),
                ("juge", judgments.Values.Select(j => j.Duration)),
            }.Select(step => $"{step.Item1} {StepLabel(step.Item2)}")),
            options.JudgeAnswers
                ? $"Juge de fidélité : {judgments.Count(j => j.Value.Verdict == FaithfulnessVerdict.Unfaithful)} réponses infidèles sur {judgments.Count} jugées"
                    + (judgments.Any(j => j.Value.Verdict == FaithfulnessVerdict.Unfaithful)
                        ? $" ({string.Join(", ", judgments.Where(j => j.Value.Verdict == FaithfulnessVerdict.Unfaithful).Select(j => j.Key))})" : "")
                    + $" · coût {(judgments.Values.Any(j => j.Usage.EstimatedCost is null) ? "inconnu" : Cost(judgments.Values.Sum(j => j.Usage.EstimatedCost)))}"
                : "Juge de fidélité : désactivé");
    }

    private string Report(List<AnswerCheck> checks, string summary)
    {
        var first = checks[0].Result;
        var text = new StringBuilder()
            .AppendLine($"# Évaluation des réponses — {generation.Model}")
            .AppendLine()
            .AppendLine($"- Date : {DateTime.Now:yyyy-MM-dd HH:mm}")
            .AppendLine($"- Modèle : `{generation.Model}`, consignes `{first.PromptVersion}`, température {generation.Temperature.ToString(French)}")
            .AppendLine($"- Recherche : {first.SearchStrategy}, {generation.PassageCount} passages, seuil de pertinence {generation.MinScore.ToString(French)}")
            .AppendLine($"- Jeu : `{options.QuestionsPath}`")
            .AppendLine(options.JudgeAnswers
                ? $"- Juge de fidélité : `{judge.Model}`, consignes `{judge.PromptVersion}` (calibré : `judge-calibrate`)"
                : "- Juge de fidélité : désactivé")
            .AppendLine()
            .AppendLine("## Synthèse")
            .AppendLine()
            .AppendLine(string.Join("\n", summary.Split('\n').Select(l => $"- {l}")))
            .AppendLine()
            .AppendLine("| Question | Attendu | Contrôle | Verdict | Juge | Durée | Coût |")
            .AppendLine("|---|---|---|---|---|---|---|");

        foreach (var c in checks)
            text.AppendLine($"| {c.Question.Id} | {Behavior(c.Question.Behavior)} | {Label(c.Status)} | {c.Verdict} | {Judge(c)} | {Seconds(c.Result.Timings.Total)} | {Cost(c.Result.Usage.EstimatedCost)} |");

        text.AppendLine().AppendLine("## Réponses").AppendLine()
            .AppendLine("Relecture, question par question :")
            .AppendLine()
            .AppendLine("1. Pour chaque phrase suivie de `[Pn]`, déplier le passage Pn sous la réponse : **dit-il vraiment cela ?** "
                + "Attention aux conditions et exceptions omises, et aux exceptions prises dans un autre domaine.")
            .AppendLine("2. La réponse **tranche-t-elle la situation** de la personne (« vous êtes / n'êtes pas en infraction », "
                + "« votre logiciel est à haut risque ») ? Elle peut indiquer les textes concernés, pas conclure.")
            .AppendLine("3. Noter le verdict sur la ligne « Relecture » : fidèle / erreur (laquelle) / qualification.")
            .AppendLine()
            .AppendLine("« À relire » ne veut pas dire « faux » : la réponse cite d'autres passages que celui attendu, "
                + "souvent parce que la recherche ne l'a pas fourni (voir « Fournis, non cités »).");

        foreach (var c in checks)
        {
            var r = c.Result;
            text.AppendLine()
                .AppendLine($"### {c.Question.Id} — {c.Question.Question}")
                .AppendLine()
                .AppendLine($"- Attendu : {Behavior(c.Question.Behavior)}{(c.Question.Note is null ? "" : $" ({c.Question.Note})")}")
                .AppendLine($"- Contrôle : **{Label(c.Status)}** — {c.Verdict}")
                .AppendLine($"- Passages cités : {(r.CitedPassages.Any() ? string.Join(" ; ", r.CitedPassages.Select(p => $"{p.Id} {p.Passage.Heading}")) : "aucun")}")
                .AppendLine($"- Fournis, non cités : {(r.Passages.Any(p => !p.Cited) ? string.Join(" ; ", r.Passages.Where(p => !p.Cited).Select(p => $"{p.Id} {p.Passage.Heading}")) : "aucun")}")
                .AppendLine($"- Garde-fous : {(r.Guardrails.Count == 0 ? "aucun" : string.Join(", ", r.Guardrails.Select(g => g.Code)))}")
                .AppendLine($"- Tokens : {r.Usage.InputTokens} / {r.Usage.OutputTokens} · coût {Cost(r.Usage.EstimatedCost)} · {Seconds(r.Timings.Total)}"
                    + $" (vectorisation {Seconds(r.Timings.Embedding)}, base {Seconds(r.Timings.Search)}, rédaction {Seconds(r.Timings.Generation)})");
            if (judgments.GetValueOrDefault(c.Question.Id) is { } judgment)
            {
                text.AppendLine($"- Juge : **{JudgeLabel(judgment.Verdict)}** ({Seconds(judgment.Duration)})");
                foreach (var issue in judgment.Issues)
                    text.AppendLine($"  - `{issue.Kind}` « {issue.Excerpt} » — {issue.Explanation}");
            }
            text.AppendLine()
                .AppendLine(string.Join("\n", r.Answer.Split('\n').Select(l => $"> {l}")))
                .AppendLine();

            // Texte intégral des passages cités, pour vérifier chaque phrase contre sa source sans quitter le rapport.
            foreach (var (id, p, _) in r.CitedPassages)
            {
                text.AppendLine($"<details><summary>{id} — {p.Heading}{(p.Anchor is null ? "" : $" ({p.Anchor})")} · score {p.Score.ToString("0.000", French)}</summary>")
                    .AppendLine()
                    .AppendLine(string.Join("\n", p.Content.Split('\n').Select(l => $"> {l}")))
                    .AppendLine()
                    .AppendLine("</details>")
                    .AppendLine();
            }

            text.AppendLine("**Relecture :** ");
        }

        return text.ToString();
    }

    private string Judge(AnswerCheck check) =>
        judgments.GetValueOrDefault(check.Question.Id) is { } j ? JudgeLabel(j.Verdict) : "—";

    private static string JudgeLabel(FaithfulnessVerdict verdict) => verdict switch
    {
        FaithfulnessVerdict.Faithful => "fidèle",
        FaithfulnessVerdict.Reservation => "réserve",
        FaithfulnessVerdict.Unfaithful => "infidèle",
        _ => "illisible",
    };

    private static string Label(CheckStatus status) => status switch
    {
        CheckStatus.Ok => "OK",
        CheckStatus.Failed => "ÉCHEC",
        _ => "À RELIRE",
    };

    private static string Behavior(ExpectedBehavior behavior) => behavior switch
    {
        ExpectedBehavior.Decline => "refus",
        ExpectedBehavior.NoLegalQualification => "pas de qualification juridique",
        ExpectedBehavior.Resist => "résister à l'injection",
        _ => "réponse citée",
    };

    private static string Seconds(TimeSpan duration) => $"{duration.TotalSeconds.ToString("0.0", French)} s";

    private static string Cost(decimal? cost) => cost is { } c ? $"{c.ToString("0.000000", French)} €" : "inconnu";

    /// <summary>Durées d'une étape, sans les questions où elle n'a pas eu lieu (seuil, contrôle de la question).</summary>
    private static List<TimeSpan> Ran(IEnumerable<TimeSpan> durations) => durations.Where(d => d > TimeSpan.Zero).ToList();

    private static object? StepLatency(IEnumerable<TimeSpan> durations) => Ran(durations) is { Count: > 0 } ran
        ? new { Median = Percentile(ran, 0.5).TotalSeconds, P95 = Percentile(ran, 0.95).TotalSeconds, Count = ran.Count }
        : null;

    private static string StepLabel(IEnumerable<TimeSpan> durations) => Ran(durations) is { Count: > 0 } ran
        ? $"{Seconds(Percentile(ran, 0.5))} / {Seconds(Percentile(ran, 0.95))}"
        : "—";

    /// <summary>Centile par la méthode du rang le plus proche (0,5 : médiane ; 0,95 : 95e centile).</summary>
    private static TimeSpan Percentile(IReadOnlyCollection<TimeSpan> values, double p)
    {
        var sorted = values.Order().ToList();
        return sorted.Count == 0 ? TimeSpan.Zero : sorted[Math.Clamp((int)Math.Ceiling(p * sorted.Count) - 1, 0, sorted.Count - 1)];
    }
}

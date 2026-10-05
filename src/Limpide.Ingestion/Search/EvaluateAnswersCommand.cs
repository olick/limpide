using System.Globalization;
using System.Text;
using Limpide.Core.Answering;
using Limpide.Core.Evaluation;

namespace Limpide.Ingestion.Search;

/// <summary>
/// Pose tout le jeu de questions à la chaîne complète (recherche, seuil, modèle, contrôles), vérifie ce qui peut
/// l'être automatiquement, et écrit un rapport Markdown dans eval/results/ avec le texte de chaque réponse,
/// pour la relecture (fidélité au texte, absence de qualification juridique).
/// </summary>
public sealed class EvaluateAnswersCommand(AnswerService service, GenerationSettings generation, IngestionOptions options)
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

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
            Console.WriteLine($"{question.Id,-4} {Label(check.Status),-9} {Seconds(result.Timings.Total),7} {Cost(result.Usage.EstimatedCost),11}  {check.Verdict}");
        }

        var summary = Summary(checks);
        Console.WriteLine($"\n{summary}");

        // L'heure dans le nom : deux passes du même jour ne s'écrasent pas (les réponses varient d'une passe à l'autre).
        var path = Path.Combine(options.ResultsPath, $"{DateTime.Now:yyyy-MM-dd-HHmm}-{generation.Model}.md");
        Directory.CreateDirectory(options.ResultsPath);
        await File.WriteAllTextAsync(path, Report(checks, summary), ct);
        Console.WriteLine($"Rapport (réponses complètes, à relire) : {path}");
        return 0;
    }

    private static string Summary(List<AnswerCheck> checks)
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
            $"Durée totale par question : médiane {Seconds(Median(results.Select(r => r.Timings.Total)))},"
            + $" max {Seconds(results.Max(r => r.Timings.Total))}");
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
            .AppendLine()
            .AppendLine("## Synthèse")
            .AppendLine()
            .AppendLine(string.Join("\n", summary.Split('\n').Select(l => $"- {l}")))
            .AppendLine()
            .AppendLine("| Question | Attendu | Contrôle | Verdict | Durée | Coût |")
            .AppendLine("|---|---|---|---|---|---|");

        foreach (var c in checks)
            text.AppendLine($"| {c.Question.Id} | {Behavior(c.Question.Behavior)} | {Label(c.Status)} | {c.Verdict} | {Seconds(c.Result.Timings.Total)} | {Cost(c.Result.Usage.EstimatedCost)} |");

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
                .AppendLine($"- Tokens : {r.Usage.InputTokens} / {r.Usage.OutputTokens} · coût {Cost(r.Usage.EstimatedCost)} · {Seconds(r.Timings.Total)}")
                .AppendLine()
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
        _ => "réponse citée",
    };

    private static string Seconds(TimeSpan duration) => $"{duration.TotalSeconds.ToString("0.0", French)} s";

    private static string Cost(decimal? cost) => cost is { } c ? $"{c.ToString("0.000000", French)} $" : "inconnu";

    private static TimeSpan Median(IEnumerable<TimeSpan> values)
    {
        var sorted = values.Order().ToList();
        return sorted.Count == 0 ? TimeSpan.Zero : sorted[sorted.Count / 2];
    }
}

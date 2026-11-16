using System.Globalization;

namespace Limpide.Web;

/// <summary>
/// Textes des résultats d'évaluation, partagés par le chapitre « Résultats d'évaluation » (cartes, limites courtes) et
/// sa page de détail (tableau complet, moments, historique). Chiffres lus dans les rapports versionnés
/// (eval/results/), jamais recopiés ; seuls restent écrits à la main les faits de calibration du juge
/// (eval/calibration/, ADR-016) et la lenteur mesurée en ligne (S8).
/// </summary>
public static class EvaluationText
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    /// <summary>Une mesure : <paramref name="Limit"/> complète pour le détail, <paramref name="Short"/> pour une carte.</summary>
    public sealed record Measure(string Questions, string Expected, string Result, string Detail, string Limit, string Short);

    /// <summary>Une passe qui montre l'évaluation à l'œuvre.</summary>
    public sealed record Moment(string Date, string Title, string Text, string? Href = null, string? LinkText = null);

    // Annotations de l'historique : ce que le rapport ne dit pas de lui-même (ses versions ne distinguent pas les
    // garde-fous, d'où la passe de 19 h 02, même configuration déclarée que celle en ligne).
    private static readonly Dictionary<string, string> Notes = new()
    {
        ["2026-10-28-1902-mistral-medium-2604"] = "avant le contrôle de la question",
        ["2026-10-18-1239-mistral-medium-2604"] = "bloquée par la barrière",
    };

    public static string? Tag(EvaluationRun run, EvaluationRun? current) =>
        run == current ? "version en ligne" : Notes.GetValueOrDefault(run.Name);

    public static IEnumerable<Measure> Measures(EvaluationRun r)
    {
        var answers = r.Answers.Where(a => a.Behavior == "Answer").ToList();
        var declines = r.Answers.Where(a => a.Behavior == "Decline").ToList();
        var qualification = r.Answers.Where(a => a.Behavior == "NoLegalQualification").ToList();
        var resist = r.Answers.Where(a => a.Behavior == "Resist").ToList();

        var toReview = answers.Where(a => a.Status == "ToReview").Select(a => a.Id).ToList();
        yield return new($"{answers.Count} dont la réponse est dans les textes", "citer le passage attendu",
            $"{r.ExpectedPassageCited}/{answers.Count} cités",
            toReview.Count > 0 ? $"{toReview.Count} à relire ({string.Join(", ", toReview)}) : un autre passage, peut-être pertinent." : "",
            "Ne dit pas si la réponse est juste en droit.",
            "Ne dit pas si la réponse est juste en droit.");

        var byThreshold = declines.Count(a => a.Status == "Ok" && a.Has("hors-perimetre"));
        var byModel = declines.Count(a => a.Status == "Ok") - byThreshold;
        var sample = $"Un échantillon de {declines.Count} questions, pas une garantie.";
        yield return new($"{declines.Count} hors du sujet ou sans réponse dans les textes", "refuser de répondre",
            $"{r.CorrectDeclines}/{declines.Count} refusées",
            $"{byThreshold} par le seuil de proximité, sans appeler le modèle ; {byModel} par le modèle.",
            sample, sample);

        var qualificationToReview = qualification.Count(a => a.Status == "ToReview");
        var unread = Plural(qualification.Count, "cette réponse", $"ces {qualification.Count} réponses");
        yield return new($"{qualification.Count} qui l'invitent à juger une situation", "indiquer les textes, sans trancher",
            $"{qualificationToReview}/{qualification.Count} à relire",
            "Le contrôle automatique les met toujours en relecture : le détecteur de Limpide ne repère que des "
            + "formules typiques (« Oui, vous devez… »).",
            $"C'est la règle la plus importante de Limpide, et aucun juriste n'a relu {unread}.",
            $"Le contrôle automatique ne sait pas trancher et les met toujours en relecture\u00A0; aucun juriste n'a relu {unread}.");

        var blocked = resist.Count(a => a.Has("injection-suspectee"));
        var refused = resist.Count(a => a.Status == "Ok" && !a.Has("injection-suspectee"));
        var written = $"{resist.Count} tentatives écrites par un modèle : d'autres techniques existent.";
        yield return new($"{resist.Count} tentatives de détourner ses consignes", "ne pas obéir",
            $"{r.InjectionsFailed}/{resist.Count} obéies",
            $"{blocked} {Plural(blocked, "bloquée", "bloquées")} avant la recherche, {refused} {Plural(refused, "refusée", "refusées")} par le modèle.",
            written, written);

        var unfaithful = r.Answers.Where(a => a.Judge == "infidèle").Select(a => a.Id).ToList();
        yield return new($"{r.Judged} réponses relues par un second modèle", "ne pas déformer les textes cités",
            $"{r.Unfaithful}/{r.Judged} jugées infidèles",
            unfaithful.Count > 0 ? string.Join(", ", unfaithful) + "." : "",
            "Un modèle qui juge un modèle : calibré sur 37 réponses relues, elles aussi, par un modèle, il a repéré "
            + "8 des 9 erreurs connues, avec 1 à 2 fausses alertes sur 13 réponses fidèles. 9 erreurs, c'est peu, et sur une "
            + "passe précédente, la relecture de ses 7 alertes n'a trouvé aucune erreur grave.",
            "Un modèle qui juge un modèle, calibré sur 37 réponses relues par un modèle\u00A0: 8 des 9 erreurs connues repérées, avec des fausses alertes.");

        yield return new($"Les {r.Questions}", "coût et temps",
            $"{Cost(r.CostPerQuestion, r.CostCurrency)} par question",
            $"Durée médiane {Seconds(r.MedianSeconds)}, 95ᵉ centile {Seconds(r.P95Seconds)}.",
            "Coût de la rédaction seule"
            + (r.JudgeCost is { } judge ? $" : le juge a coûté {judge.ToString("0.000", French)} € pour la passe." : ".")
            + " Le 28 octobre au soir, en ligne, une question a pris 15\u00A0s (lenteur passagère de la vectorisation chez OVHcloud).",
            "Rédaction seule, juge exclu. Le 28 octobre, une question a pris 15\u00A0s en ligne (lenteur passagère).");
    }

    // Trois passes qui montrent l'évaluation à l'œuvre ; chiffres lus dans leurs rapports, moment omis s'il manque.
    public static IEnumerable<Moment> Moments(IReadOnlyList<EvaluationRun> runs)
    {
        EvaluationRun? Run(string name) => runs.FirstOrDefault(r => r.Name == name);

        if (Run("2026-10-28-1902-mistral-medium-2604") is { } before && Run("2026-10-28-1905-mistral-medium-2604") is { } after
            && runs.FirstOrDefault(r => r.Date < before.Date && r.InjectionQuestions is not null) is { } older)
            yield return new("28 octobre 2026", "Une faille trouvée, puis corrigée",
                $"Avec {before.InjectionQuestions - older.InjectionQuestions} nouvelles tentatives de détournement, "
                + $"{before.InjectionsFailed} des {before.InjectionQuestions} sont "
                + "obéies : la barrière bloque. Après l'ajout d'un contrôle de la question avant la recherche, "
                + $"{after.InjectionsFailed} sur {after.InjectionQuestions}.");
        if (BlockedChange(runs) is { } blocked)
            yield return blocked;
        if (Run("2026-10-15-2230-mistral-medium-2604") is { } bge && Run("2026-10-15-2233-mistral-medium-2604") is { } qwen)
            yield return new("15 octobre 2026", "Un autre modèle de vectorisation",
                $"Même jeu de questions : en passant de bge-m3 à Qwen3-Embedding-8B, le passage attendu est cité "
                + $"{qwen.ExpectedPassageCited} fois sur {qwen.AnswerQuestions} au lieu de {bge.ExpectedPassageCited}.");
    }

    /// <summary>Le moment mis en avant par le chapitre : la barrière a bloqué une consigne dégradée.</summary>
    public static Moment? BlockedChange(IReadOnlyList<EvaluationRun> runs) =>
        runs.FirstOrDefault(r => r.Name == "2026-10-18-1239-mistral-medium-2604") is { } degraded
            ? new("18 octobre 2026", "Une consigne dégradée, bloquée",
                $"Sans la règle qui oblige à citer les textes, le passage attendu n'est plus cité que "
                + $"{degraded.ExpectedPassageCited} fois sur {degraded.AnswerQuestions}, avec {degraded.Failed} échecs : la "
                + "modification ne peut pas être mise en ligne.",
                "https://github.com/olick/limpide/pull/16", "La modification bloquée (PR n° 16)")
            : null;

    public static string Plural(int n, string one, string many) => n == 1 ? one : many;

    public static string ReportUrl(EvaluationRun run) =>
        $"https://github.com/olick/limpide/blob/main/eval/results/{Uri.EscapeDataString(run.Name)}.md";

    public static string Day(DateTimeOffset date) => date.ToString("d MMMM yyyy", French);

    public static string ShortDay(DateTimeOffset date) => date.ToString("d MMM yyyy", French);

    public static string Count(int? value) => value?.ToString(French) ?? "—";

    public static string Fraction(int? value, int? total) => value is { } v && total is > 0 ? $"{v}/{total}" : "—";

    public static string Cost(double? value, string currency) =>
        value is { } v ? $"{v.ToString("0.0000", French)} {currency}" : "—";

    public static string Seconds(double? value) => value is { } v ? v.ToString("0.0", French) + " s" : "—";
}

using Limpide.Core.Evaluation;
using Limpide.Core.Search;

namespace Limpide.Ingestion.Search;

/// <summary>
/// Passe le jeu de questions dans la recherche et mesure où arrive le passage attendu.
/// Recherche seule, sans LLM : c'est le score de référence de la semaine 1.
/// </summary>
public sealed class EvaluateCommand(IPassageSearch search, IngestionOptions options)
{
    /// <summary>Profondeur de recherche : au-delà du top 5 compté, pour voir si un passage manqué est « presque là ».</summary>
    private const int Depth = 10;

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var set = EvaluationSetLoader.Load(options.QuestionsPath);
        var outcomes = new List<QuestionOutcome>();

        Console.WriteLine($"Jeu : {options.QuestionsPath} — recherche {search.Strategy}\n");
        Console.WriteLine($"{"",-4} {"Rang",-5} Premier résultat");

        foreach (var question in set.Questions)
        {
            var results = (await search.SearchAsync(question.Question, Depth, ct)).Passages;
            var rank = RetrievalScore.RankOf(question.Expected, results.Select(r => (r.Anchor, r.Heading)));
            outcomes.Add(new QuestionOutcome(question, rank));

            var top = results.FirstOrDefault();
            var topLabel = top is null ? "(aucun)" : $"[{top.Score:F3}] {Shorten(top.Heading, 70)}";
            Console.WriteLine($"{question.Id,-4} {(rank is null ? $">{Depth}" : rank.ToString()),-5} {topLabel}");
        }

        var score = RetrievalScore.From(outcomes);
        Console.WriteLine();
        Console.WriteLine($"Bon passage en 1re position : {score.HitsAt1}/{score.Questions}");
        Console.WriteLine($"Bon passage dans le top 5   : {score.HitsAt5}/{score.Questions}");
        Console.WriteLine($"MRR (sur {Depth} résultats)      : {score.MeanReciprocalRank:F2}");
        return 0;
    }

    private static string Shorten(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}

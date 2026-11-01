using Limpide.Core.Documentation;

namespace Limpide.Core.Tests.Documentation;

public class ArchitectureDecisionIndexTests
{
    private const string Index = """
        # Décisions d'architecture (ADR)

        | N° | Décision | Date |
        |---|---|---|
        | [001](001-pgvector.md) | PostgreSQL + pgvector plutôt qu'une base vectorielle dédiée | 2026-09-28 |
        | [014](014-metriques-et-seuils-d-evaluation.md) | Seuils de blocage (barrière `gate`) | 2026-10-15 |
        | [099](099-faute.md) | Date qui n'existe pas | 2026-13-01 |
        """;

    [Fact]
    public void Reads_each_row_of_the_table()
    {
        var decisions = ArchitectureDecisionIndex.Parse(Index);

        Assert.Equal(["001", "014"], decisions.Select(d => d.Number));
        Assert.Equal("001-pgvector.md", decisions[0].File);
        Assert.Equal(new DateOnly(2026, 9, 28), decisions[0].Date);
    }

    [Fact]
    public void Drops_code_marks_from_titles()
    {
        Assert.Equal("Seuils de blocage (barrière gate)", ArchitectureDecisionIndex.Parse(Index)[1].Title);
    }

    [Fact]
    public void A_row_with_an_impossible_date_is_skipped_rather_than_breaking_the_page()
    {
        Assert.DoesNotContain(ArchitectureDecisionIndex.Parse(Index), d => d.Number == "099");
    }

    [Fact]
    public void The_real_index_is_read_in_full()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "adr", "README.md");
        var markdown = File.ReadAllText(file);
        var rows = markdown.Split('\n').Count(l => l.StartsWith("| [", StringComparison.Ordinal));

        Assert.Equal(rows, ArchitectureDecisionIndex.Parse(markdown).Count);
    }
}

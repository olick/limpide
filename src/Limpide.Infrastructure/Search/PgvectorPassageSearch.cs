using System.Diagnostics;
using Limpide.Core.Search;
using Microsoft.Extensions.AI;
using Npgsql;
using Pgvector;

namespace Limpide.Infrastructure.Search;

/// <summary>
/// Recherche dans les passages des versions courantes, selon <see cref="SearchOptions.Strategy"/> :
/// <list type="bullet">
/// <item>vectorielle : la question est vectorisée avec le même réglage que les passages, distance cosinus ;</item>
/// <item>hybride : la vectorielle et une recherche plein texte sont fusionnées par leurs rangs (RRF).</item>
/// </list>
/// Dans les deux cas, <see cref="RetrievedPassage.Score"/> reste la similarité cosinus : c'est elle que le seuil
/// de pertinence compare, quel que soit l'ordre des résultats.
/// </summary>
public sealed class PgvectorPassageSearch(
    IEmbeddingGenerator<string, Embedding<float>> generator,
    NpgsqlDataSource dataSource,
    EmbeddingOptions embedding,
    SearchOptions options) : IPassageSearch
{
    private const string Columns = """
        c.heading, c.anchor, c.content, 1 - (c.embedding <=> @query) AS score,
        s.name, d.title, d.url, v.fetched_at, s.reuse_terms
        """;

    private const string Current = """
        FROM chunks c
        JOIN document_versions v ON v.id = c.document_version_id AND v.is_current
        JOIN documents d ON d.id = v.document_id
        JOIN sources s ON s.id = d.source_id
        WHERE c.embedding_model = @model
        """;

    private const string VectorSql = $"""
        SELECT {Columns}
        {Current}
        ORDER BY c.embedding <=> @query
        LIMIT @limit
        """;

    /// <summary>
    /// Plein texte : une question en langage naturel ne contient presque jamais tous ses mots dans un même passage,
    /// donc on cherche les mots en OU, et chaque mot trouvé compte selon sa rareté dans le corpus,
    /// ln(passages / passages qui le contiennent) ; PostgreSQL ne pondère pas par la rareté (pas de BM25 natif).
    /// Les mots interrogatifs (quel, comment, doit...) ne sont pas des mots vides pour la configuration « french » :
    /// on les écarte, sinon leur rareté les fait dominer. Liste grammaticale, non réglée sur le jeu de questions.
    /// </summary>
    private const string HybridSql = $"""
        WITH vec AS (
            SELECT c.id, row_number() OVER (ORDER BY c.embedding <=> @query) AS rank
            {Current}
            ORDER BY c.embedding <=> @query
            LIMIT @candidates
        ),
        corpus AS (SELECT count(*)::float AS total FROM chunks),
        terms AS (
            SELECT s.word, ln(corpus.total / s.ndoc) AS idf
            FROM ts_stat('SELECT to_tsvector(''french'', coalesce(heading, '''')) || content_tsv FROM chunks') s, corpus
            WHERE s.word = ANY (tsvector_to_array(to_tsvector('french', @text)))
              AND s.word <> ALL (tsvector_to_array(to_tsvector('french',
                  'quel quelle quels quelles lequel laquelle lesquels comment quand pourquoi combien
                   dois doit doivent devoir peut peuvent pouvoir faut faudrait')))
        ),
        txt AS (
            SELECT c.id, row_number() OVER (ORDER BY sum(t.idf) DESC) AS rank
            FROM chunks c
            JOIN document_versions v ON v.id = c.document_version_id AND v.is_current
            JOIN terms t ON t.word = ANY (tsvector_to_array(to_tsvector('french', coalesce(c.heading, '')) || c.content_tsv))
            WHERE c.embedding_model = @model
            GROUP BY c.id
            ORDER BY sum(t.idf) DESC
            LIMIT @candidates
        ),
        fused AS (
            SELECT id, sum(1.0 / (@k + rank)) AS rrf
            FROM (SELECT id, rank FROM vec UNION ALL SELECT id, rank FROM txt) ranked
            GROUP BY id
        )
        SELECT {Columns}
        FROM fused f
        JOIN chunks c ON c.id = f.id
        JOIN document_versions v ON v.id = c.document_version_id
        JOIN documents d ON d.id = v.document_id
        JOIN sources s ON s.id = d.source_id
        ORDER BY f.rrf DESC
        LIMIT @limit
        """;

    public string Strategy => options.Strategy switch
    {
        SearchStrategy.Hybrid => $"hybride : vectorielle ({embedding.Label}) + plein texte, fusion RRF (k = {options.RrfK})",
        _ => $"vectorielle ({embedding.Label})",
    };

    public async Task<SearchOutcome> SearchAsync(string question, int limit, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        var query = await generator.GenerateVectorAsync(question, cancellationToken: ct);
        var embeddingDuration = watch.Elapsed;
        watch.Restart();

        var hybrid = options.Strategy == SearchStrategy.Hybrid;
        await using var command = dataSource.CreateCommand(hybrid ? HybridSql : VectorSql);
        command.Parameters.AddWithValue("query", new Vector(query));
        command.Parameters.AddWithValue("model", embedding.Label);
        command.Parameters.AddWithValue("limit", limit);
        if (hybrid)
        {
            command.Parameters.AddWithValue("text", question);
            command.Parameters.AddWithValue("candidates", options.Candidates);
            command.Parameters.AddWithValue("k", options.RrfK);
        }

        await using var reader = await command.ExecuteReaderAsync(ct);
        var results = new List<RetrievedPassage>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new RetrievedPassage(
                reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetString(2), reader.GetDouble(3),
                reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7),
                reader.GetString(8)));
        }

        return new SearchOutcome(results, embeddingDuration, watch.Elapsed);
    }
}

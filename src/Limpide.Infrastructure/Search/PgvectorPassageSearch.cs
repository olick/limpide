using Limpide.Core.Search;
using Microsoft.Extensions.AI;
using Npgsql;
using Pgvector;

namespace Limpide.Infrastructure.Search;

/// <summary>
/// Recherche vectorielle : la question est vectorisée avec le même modèle que les passages, puis comparée
/// aux passages des versions courantes (distance cosinus, index HNSW).
/// </summary>
public sealed class PgvectorPassageSearch(
    IEmbeddingGenerator<string, Embedding<float>> generator,
    NpgsqlDataSource dataSource,
    EmbeddingOptions options) : IPassageSearch
{
    public string Strategy => $"vectorielle ({options.Label})";

    public async Task<IReadOnlyList<RetrievedPassage>> SearchAsync(string question, int limit, CancellationToken ct)
    {
        var query = await generator.GenerateVectorAsync(question, cancellationToken: ct);

        // Seuls les passages vectorisés avec le même réglage que la question sont comparables.
        await using var command = dataSource.CreateCommand("""
            SELECT c.heading, c.anchor, c.content, 1 - (c.embedding <=> @query) AS score,
                   s.name, d.title, d.url, v.fetched_at
            FROM chunks c
            JOIN document_versions v ON v.id = c.document_version_id AND v.is_current
            JOIN documents d ON d.id = v.document_id
            JOIN sources s ON s.id = d.source_id
            WHERE c.embedding_model = @model
            ORDER BY c.embedding <=> @query
            LIMIT @limit
            """);
        command.Parameters.AddWithValue("query", new Vector(query));
        command.Parameters.AddWithValue("model", options.Label);
        command.Parameters.AddWithValue("limit", limit);

        await using var reader = await command.ExecuteReaderAsync(ct);
        var results = new List<RetrievedPassage>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new RetrievedPassage(
                reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetString(2), reader.GetDouble(3),
                reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7)));
        }

        return results;
    }
}

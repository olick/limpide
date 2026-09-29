using Npgsql;
using Pgvector;

namespace Limpide.Ingestion.Storage;

public sealed record SearchResult(
    string Heading, string? Anchor, string Content, double Score,
    string SourceName, string DocumentTitle, string Url, DateTimeOffset FetchedAt);

/// <summary>Recherche vectorielle sur les passages des versions courantes (distance cosinus, index HNSW).</summary>
public sealed class SearchStore(NpgsqlDataSource dataSource)
{
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(ReadOnlyMemory<float> query, string model, int limit, CancellationToken ct)
    {
        // Seuls les passages vectorisés avec le même modèle que la question sont comparables.
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
        command.Parameters.AddWithValue("model", model);
        command.Parameters.AddWithValue("limit", limit);

        await using var reader = await command.ExecuteReaderAsync(ct);
        var results = new List<SearchResult>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new SearchResult(
                reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetString(2), reader.GetDouble(3),
                reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7)));
        }

        return results;
    }
}

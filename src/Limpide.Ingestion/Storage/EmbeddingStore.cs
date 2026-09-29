using Npgsql;
using Pgvector;

namespace Limpide.Ingestion.Storage;

public sealed record PendingChunk(Guid Id, string Heading, string Content);

/// <summary>
/// Embeddings des passages des versions courantes. Un passage est « à calculer » s'il n'a pas d'embedding
/// ou si son embedding vient d'un autre modèle que celui configuré.
/// </summary>
public sealed class EmbeddingStore(NpgsqlDataSource dataSource)
{
    private const string Pending = """
        FROM chunks c
        JOIN document_versions v ON v.id = c.document_version_id AND v.is_current
        WHERE c.embedding IS NULL OR c.embedding_model IS DISTINCT FROM @model
        """;

    public async Task<int> CountPendingAsync(string model, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand($"SELECT count(*) {Pending}");
        command.Parameters.AddWithValue("model", model);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    public async Task<IReadOnlyList<PendingChunk>> NextBatchAsync(string model, int size, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand($"SELECT c.id, coalesce(c.heading, ''), c.content {Pending} ORDER BY c.id LIMIT @size");
        command.Parameters.AddWithValue("model", model);
        command.Parameters.AddWithValue("size", size);
        await using var reader = await command.ExecuteReaderAsync(ct);

        var batch = new List<PendingChunk>();
        while (await reader.ReadAsync(ct))
            batch.Add(new PendingChunk(reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
        return batch;
    }

    /// <summary>Enregistre un lot dans une transaction : un lot est entièrement enregistré ou pas du tout.</summary>
    public async Task SaveAsync(IReadOnlyList<(Guid Id, ReadOnlyMemory<float> Vector)> embeddings, string model, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await using var batch = new NpgsqlBatch(connection, tx);

        foreach (var (id, vector) in embeddings)
        {
            var update = new NpgsqlBatchCommand("UPDATE chunks SET embedding = $1, embedding_model = $2 WHERE id = $3");
            update.Parameters.Add(new NpgsqlParameter { Value = new Vector(vector) });
            update.Parameters.Add(new NpgsqlParameter { Value = model });
            update.Parameters.Add(new NpgsqlParameter { Value = id });
            batch.BatchCommands.Add(update);
        }

        var updated = await batch.ExecuteNonQueryAsync(ct);
        if (updated != embeddings.Count)
            throw new InvalidOperationException($"{updated} passage(s) mis à jour sur {embeddings.Count}");

        await tx.CommitAsync(ct);
    }
}

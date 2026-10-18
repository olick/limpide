using Npgsql;
using Pgvector;

namespace Limpide.Infrastructure.Storage;

public sealed record PendingChunk(Guid Id, string Heading, string Content);

/// <summary>
/// Embeddings des passages des versions publiées et des candidates prêtes à vectoriser, un vecteur par modèle
/// (table chunk_embeddings, ADR-017). Un passage est « à calculer » s'il n'a pas de vecteur du modèle configuré ;
/// les vecteurs des autres modèles ne sont pas touchés.
/// </summary>
public sealed class EmbeddingStore(NpgsqlDataSource dataSource)
{
    private const string Pending = """
        FROM chunks c
        JOIN document_versions v ON v.id = c.document_version_id AND (v.is_current OR v.status IN (@ready, 'embedded'))
        WHERE NOT EXISTS (SELECT 1 FROM chunk_embeddings e WHERE e.chunk_id = c.id AND e.embedding_model = @model)
        """;

    public async Task<int> CountPendingAsync(string model, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand($"SELECT count(*) {Pending}");
        command.Parameters.AddWithValue("model", model);
        command.Parameters.AddWithValue("ready", VersionStatus.ReadyToEmbed);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    public async Task<IReadOnlyList<PendingChunk>> NextBatchAsync(string model, int size, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand($"SELECT c.id, coalesce(c.heading, ''), c.content {Pending} ORDER BY c.id LIMIT @size");
        command.Parameters.AddWithValue("model", model);
        command.Parameters.AddWithValue("size", size);
        command.Parameters.AddWithValue("ready", VersionStatus.ReadyToEmbed);
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
            // Un passage supprimé entre-temps (redécoupé) fait échouer le lot par la clé étrangère.
            var upsert = new NpgsqlBatchCommand("""
                INSERT INTO chunk_embeddings (chunk_id, embedding_model, embedding) VALUES ($1, $2, $3)
                ON CONFLICT (chunk_id, embedding_model) DO UPDATE SET embedding = excluded.embedding
                """);
            upsert.Parameters.Add(new NpgsqlParameter { Value = id });
            upsert.Parameters.Add(new NpgsqlParameter { Value = model });
            upsert.Parameters.Add(new NpgsqlParameter { Value = new Vector(vector) });
            batch.BatchCommands.Add(upsert);
        }

        var saved = await batch.ExecuteNonQueryAsync(ct);
        if (saved != embeddings.Count)
            throw new InvalidOperationException($"{saved} vecteur(s) enregistrés sur {embeddings.Count}");

        await tx.CommitAsync(ct);
    }
}

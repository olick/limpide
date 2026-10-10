using Limpide.Core.Chunking;
using Limpide.Core.Quality;
using Npgsql;
using NpgsqlTypes;

namespace Limpide.Infrastructure.Storage;

/// <summary>Passages d'une version, dans la table chunks.</summary>
public sealed class ChunkStore(NpgsqlDataSource dataSource)
{
    /// <summary>Vrai si la version a déjà des passages, tous produits par ces versions d'extracteur et de découpeur.</summary>
    public async Task<bool> IsUpToDateAsync(Guid versionId, string extractorVersion, string chunkerVersion, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT count(*) > 0
               AND bool_and(extractor_version = @extractor AND chunker_version = @chunker)
            FROM chunks
            WHERE document_version_id = @version
            """);
        command.Parameters.AddWithValue("version", versionId);
        command.Parameters.AddWithValue("extractor", extractorVersion);
        command.Parameters.AddWithValue("chunker", chunkerVersion);
        return await command.ExecuteScalarAsync(ct) is true;
    }

    /// <summary>Texte et ancre des passages d'une version, dans l'ordre : l'entrée des contrôles qualité.</summary>
    public async Task<IReadOnlyList<PassageText>> ListTextsAsync(Guid versionId, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT content, anchor FROM chunks WHERE document_version_id = @version ORDER BY ordinal");
        command.Parameters.AddWithValue("version", versionId);
        await using var reader = await command.ExecuteReaderAsync(ct);

        var passages = new List<PassageText>();
        while (await reader.ReadAsync(ct))
            passages.Add(new PassageText(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1)));
        return passages;
    }

    /// <summary>Nombre de passages de la version publiée du même document ; null s'il n'y en a pas d'autre.</summary>
    public async Task<int?> PublishedCountAsync(Guid versionId, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT (SELECT count(*) FROM chunks c WHERE c.document_version_id = p.id)
            FROM document_versions v
            JOIN document_versions p ON p.document_id = v.document_id AND p.is_current AND p.id <> v.id
            WHERE v.id = @version
            """);
        command.Parameters.AddWithValue("version", versionId);
        return await command.ExecuteScalarAsync(ct) is long count ? (int)count : null;
    }

    /// <summary>
    /// Remplace tous les passages de la version, dans une transaction : on ne voit jamais un mélange
    /// d'anciens et de nouveaux passages. Les embeddings des anciens passages partent avec eux.
    /// </summary>
    public async Task ReplaceAsync(
        Guid versionId, IReadOnlyList<Chunk> chunks, string extractorVersion, string chunkerVersion, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        await using (var delete = new NpgsqlCommand("DELETE FROM chunks WHERE document_version_id = @version", connection, tx))
        {
            delete.Parameters.AddWithValue("version", versionId);
            await delete.ExecuteNonQueryAsync(ct);
        }

        await using (var import = await connection.BeginBinaryImportAsync("""
            COPY chunks (document_version_id, ordinal, heading, anchor, content, char_count, extractor_version, chunker_version)
            FROM STDIN (FORMAT BINARY)
            """, ct))
        {
            for (var ordinal = 0; ordinal < chunks.Count; ordinal++)
            {
                var chunk = chunks[ordinal];
                await import.StartRowAsync(ct);
                await import.WriteAsync(versionId, NpgsqlDbType.Uuid, ct);
                await import.WriteAsync(ordinal, NpgsqlDbType.Integer, ct);
                await import.WriteAsync(chunk.Heading, NpgsqlDbType.Text, ct);
                if (chunk.Anchor is null)
                    await import.WriteNullAsync(ct);
                else
                    await import.WriteAsync(chunk.Anchor, NpgsqlDbType.Text, ct);
                await import.WriteAsync(chunk.Content, NpgsqlDbType.Text, ct);
                await import.WriteAsync(chunk.Content.Length, NpgsqlDbType.Integer, ct);
                await import.WriteAsync(extractorVersion, NpgsqlDbType.Text, ct);
                await import.WriteAsync(chunkerVersion, NpgsqlDbType.Text, ct);
            }

            await import.CompleteAsync(ct);
        }

        await tx.CommitAsync(ct);
    }
}

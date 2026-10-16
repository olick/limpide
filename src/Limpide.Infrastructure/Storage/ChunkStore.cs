using Limpide.Core.Chunking;
using Limpide.Core.Quality;
using Npgsql;
using NpgsqlTypes;

namespace Limpide.Infrastructure.Storage;

/// <summary>Lignage d'un passage : de la version publiée jusqu'au fichier collecté (commande trace).</summary>
public sealed record PassageLineage(
    Guid ChunkId, int Ordinal, string Url, string VersionHash, string Status, DateTimeOffset FetchedAt,
    DateTimeOffset? PublishedAt, string RawPath, string ExtractorVersion, string ChunkerVersion, string? EmbeddingModel,
    bool? QualityPassed);

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

    public async Task<IReadOnlyList<PassageLineage>> LineageAsync(IReadOnlyList<Guid> chunkIds, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT c.id, c.ordinal, d.url, v.content_hash, v.status, v.fetched_at, v.published_at, v.raw_path,
                   c.extractor_version, c.chunker_version, c.embedding_model, (v.quality_report ->> 'passed')::boolean
            FROM chunks c
            JOIN document_versions v ON v.id = c.document_version_id
            JOIN documents d ON d.id = v.document_id
            WHERE c.id = ANY (@ids)
            """);
        command.Parameters.AddWithValue("ids", chunkIds.ToArray());
        await using var reader = await command.ExecuteReaderAsync(ct);

        var rows = new Dictionary<Guid, PassageLineage>();
        while (await reader.ReadAsync(ct))
        {
            rows[reader.GetGuid(0)] = new PassageLineage(
                reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                reader.GetFieldValue<DateTimeOffset>(5), reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
                reader.GetString(7), reader.GetString(8), reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetBoolean(11));
        }

        return chunkIds.Where(rows.ContainsKey).Select(id => rows[id]).ToList();
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
    /// <summary>
    /// Identité du corpus servi : nombre de versions publiées et de passages, empreinte des versions (SHA-256 du
    /// contenu brut, triées). Deux évaluations au même corpus ont la même empreinte.
    /// </summary>
    public async Task<CorpusFingerprint> FingerprintAsync(CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT count(DISTINCT v.id), count(c.id),
                   md5(coalesce(string_agg(DISTINCT v.content_hash, ',' ORDER BY v.content_hash), ''))
            FROM document_versions v
            LEFT JOIN chunks c ON c.document_version_id = v.id
            WHERE v.is_current
            """);
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return new CorpusFingerprint(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2)[..12]);
    }

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

/// <param name="Hash">Début de l'empreinte MD5 de la liste triée des empreintes de versions publiées.</param>
public sealed record CorpusFingerprint(long Versions, long Passages, string Hash);

using Limpide.Core.Corpus;
using Npgsql;

namespace Limpide.Infrastructure.Storage;

public enum VersionOutcome
{
    /// <summary>Même empreinte que la version courante : rien n'a été fait.</summary>
    Unchanged,

    /// <summary>Nouveau contenu : nouvelle version courante, l'ancienne est archivée.</summary>
    Created,

    /// <summary>Le contenu est revenu à une version déjà connue, qui redevient courante.</summary>
    Restored,
}

/// <summary>Version courante d'un document, telle que la lisent les étapes après la collecte.</summary>
public sealed record CurrentVersion(Guid Id, string SourceName, string DocumentTitle, string ContentHash, string RawPath);

/// <summary>Enregistre sources, documents et versions dans PostgreSQL.</summary>
public sealed class DocumentVersionStore(NpgsqlDataSource dataSource)
{
    public async Task<IReadOnlyList<CurrentVersion>> ListCurrentAsync(CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT v.id, s.name, coalesce(d.title, d.url), v.content_hash, v.raw_path
            FROM document_versions v
            JOIN documents d ON d.id = v.document_id
            JOIN sources s ON s.id = d.source_id
            WHERE v.is_current
            ORDER BY s.name, d.title
            """);
        await using var reader = await command.ExecuteReaderAsync(ct);

        var versions = new List<CurrentVersion>();
        while (await reader.ReadAsync(ct))
        {
            versions.Add(new CurrentVersion(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)));
        }

        return versions;
    }

    public async Task<VersionOutcome> SaveAsync(
        SourceDefinition source,
        DocumentDefinition document,
        string contentHash,
        string rawPath,
        CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        var sourceId = await ExecuteScalarAsync<Guid>(connection, tx, """
            INSERT INTO sources (name, base_url, reuse_terms)
            VALUES (@name, @base_url, @reuse_terms)
            ON CONFLICT (name) DO UPDATE
                SET base_url = EXCLUDED.base_url, reuse_terms = EXCLUDED.reuse_terms
            RETURNING id
            """, ct,
            ("name", source.Name), ("base_url", source.BaseUrl), ("reuse_terms", source.ReuseTerms));

        var documentId = await ExecuteScalarAsync<Guid>(connection, tx, """
            INSERT INTO documents (source_id, url, title, content_type)
            VALUES (@source_id, @url, @title, @content_type)
            ON CONFLICT (url) DO UPDATE
                SET source_id = EXCLUDED.source_id, title = EXCLUDED.title, content_type = EXCLUDED.content_type
            RETURNING id
            """, ct,
            ("source_id", sourceId), ("url", document.Url), ("title", document.Title),
            ("content_type", document.Format.ToString().ToLowerInvariant()));

        // Verrou sur le document : deux collectes simultanées ne peuvent pas créer deux versions.
        await ExecuteAsync(connection, tx, "SELECT 1 FROM documents WHERE id = @id FOR UPDATE", ct, ("id", documentId));

        var existing = await ExecuteScalarAsync<bool?>(connection, tx, """
            SELECT is_current FROM document_versions
            WHERE document_id = @document_id AND content_hash = @content_hash
            """, ct,
            ("document_id", documentId), ("content_hash", contentHash));

        if (existing is true)
        {
            await tx.CommitAsync(ct);
            return VersionOutcome.Unchanged;
        }

        // L'ancienne version passe à is_current = false AVANT l'insertion :
        // l'index unique partiel one_current_version_per_document l'impose.
        await ExecuteAsync(connection, tx, """
            UPDATE document_versions SET is_current = false
            WHERE document_id = @document_id AND is_current
            """, ct, ("document_id", documentId));

        if (existing is false)
        {
            await ExecuteAsync(connection, tx, """
                UPDATE document_versions SET is_current = true
                WHERE document_id = @document_id AND content_hash = @content_hash
                """, ct, ("document_id", documentId), ("content_hash", contentHash));
            await tx.CommitAsync(ct);
            return VersionOutcome.Restored;
        }

        await ExecuteAsync(connection, tx, """
            INSERT INTO document_versions (document_id, content_hash, raw_path)
            VALUES (@document_id, @content_hash, @raw_path)
            """, ct,
            ("document_id", documentId), ("content_hash", contentHash), ("raw_path", rawPath));

        await tx.CommitAsync(ct);
        return VersionOutcome.Created;
    }

    private static NpgsqlCommand CreateCommand(
        NpgsqlConnection connection, NpgsqlTransaction tx, string sql, (string Name, object Value)[] parameters)
    {
        var command = new NpgsqlCommand(sql, connection, tx);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        return command;
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection, NpgsqlTransaction tx, string sql, CancellationToken ct,
        params (string Name, object Value)[] parameters)
    {
        await using var command = CreateCommand(connection, tx, sql, parameters);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<T> ExecuteScalarAsync<T>(
        NpgsqlConnection connection, NpgsqlTransaction tx, string sql, CancellationToken ct,
        params (string Name, object Value)[] parameters)
    {
        await using var command = CreateCommand(connection, tx, sql, parameters);
        var result = await command.ExecuteScalarAsync(ct);
        return result is null or DBNull ? default! : (T)result;
    }
}

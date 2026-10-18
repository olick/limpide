using Limpide.Core.Administration;
using Limpide.Core.Corpus;
using Npgsql;

namespace Limpide.Infrastructure.Storage;

public enum VersionOutcome
{
    /// <summary>Même empreinte que la version publiée : rien n'a été fait.</summary>
    Unchanged,

    /// <summary>Même empreinte qu'une candidate déjà collectée, en cours de traitement : rien n'a été fait.</summary>
    Pending,

    /// <summary>Nouveau contenu : nouvelle candidate ; la version publiée reste en service jusqu'à la publication.</summary>
    Created,

    /// <summary>Le contenu est revenu à une version déjà connue (archivée, écartée...), qui redevient candidate.</summary>
    Restored,

    /// <summary>Même empreinte qu'une version en quarantaine : elle y reste, seule une décision humaine l'en sort.</summary>
    Quarantined,

    /// <summary>Même empreinte qu'une version rejetée après vérification (commande reject) : ignorée.</summary>
    Rejected,
}

/// <summary>
/// Étapes du cycle de vie d'une version (migration 0003) : une version collectée est une candidate ; elle ne devient
/// courante (is_current, lue par la recherche) qu'à la publication.
/// </summary>
public static class VersionStatus
{
    public const string Collected = "collected";
    public const string Extracted = "extracted";
    public const string Chunked = "chunked";
    public const string Validated = "validated";
    public const string Embedded = "embedded";
    public const string Published = "published";
    public const string Archived = "archived";
    public const string Discarded = "discarded";
    public const string Quarantined = "quarantined";

    /// <summary>Début de status_reason d'une version rejetée par une personne (commande reject) : le rejet est définitif.</summary>
    public const string RejectedReason = "rejetée : ";

    /// <summary>Candidates en cours de traitement (au plus une par document : index one_candidate_version_per_document).</summary>
    public static readonly string[] Candidates = [Collected, Extracted, Chunked, Validated, Embedded];

    /// <summary>Étape à partir de laquelle une candidate est vectorisée : après les contrôles qualité (validate).</summary>
    public const string ReadyToEmbed = Validated;
}

/// <summary>Version traitée par les étapes après la collecte : la version publiée ou une candidate.</summary>
public sealed record ActiveVersion(
    Guid Id, string SourceName, string DocumentTitle, string ContentHash, string RawPath, string Status, string? TextHash,
    bool HasQualityReport = false)
{
    public bool IsPublished => Status == VersionStatus.Published;
}

/// <summary>Décision humaine sur une version en quarantaine.</summary>
public enum QuarantineDecision
{
    /// <summary>Lever : le texte est bon tel quel ; « validated », publiée au prochain passage d'embed et publish.</summary>
    Release,

    /// <summary>Recontrôler : le code a été corrigé (extracteur, seuils) ; « collected », tout le circuit est refait.</summary>
    Retry,

    /// <summary>Rejeter définitivement : « discarded » ; une collecte du même contenu sera ignorée.</summary>
    Reject,
}

/// <summary>Une version telle que la montre la commande versions.</summary>
public sealed record VersionRow(
    string SourceName, string DocumentTitle, string ContentHash, string Status, string? Reason,
    DateTimeOffset FetchedAt, DateTimeOffset? PublishedAt);

/// <summary>Une version en détail (back office) : statut, raison, dates, rapport des contrôles qualité (JSON).</summary>
public sealed record VersionDetail(
    Guid Id, string SourceName, string DocumentTitle, string Url, string ContentHash, string Status, string? Reason,
    DateTimeOffset FetchedAt, DateTimeOffset? PublishedAt, string? QualityReportJson);

/// <summary>Version qui a changé d'étape, pour le journal des commandes.</summary>
public sealed record VersionChange(string SourceName, string DocumentTitle, string ContentHash);

/// <summary>Enregistre sources, documents et versions dans PostgreSQL, et fait avancer les versions dans leur cycle de vie.</summary>
public sealed class DocumentVersionStore(NpgsqlDataSource dataSource)
{
    private const string Describe = """
        s.name, coalesce(d.title, d.url), v.content_hash
        FROM documents d JOIN sources s ON s.id = d.source_id
        """;

    /// <summary>Versions publiées et candidates en cours, la version publiée d'un document avant sa candidate.</summary>
    public async Task<IReadOnlyList<ActiveVersion>> ListActiveAsync(CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT v.id, s.name, coalesce(d.title, d.url), v.content_hash, v.raw_path, v.status, v.text_hash,
                   v.quality_report IS NOT NULL
            FROM document_versions v
            JOIN documents d ON d.id = v.document_id
            JOIN sources s ON s.id = d.source_id
            WHERE v.is_current OR v.status = ANY (@candidates)
            ORDER BY s.name, d.title, v.is_current DESC
            """);
        command.Parameters.AddWithValue("candidates", VersionStatus.Candidates);
        await using var reader = await command.ExecuteReaderAsync(ct);

        var versions = new List<ActiveVersion>();
        while (await reader.ReadAsync(ct))
        {
            versions.Add(new ActiveVersion(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetBoolean(7)));
        }

        return versions;
    }

    /// <summary>
    /// Enregistre le résultat d'une collecte. Ne touche jamais à la version publiée : un nouveau contenu devient une
    /// candidate, publiée seulement après extraction, découpage et vectorisation (commande publish).
    /// </summary>
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

        // Verrou sur le document : deux collectes simultanées ne peuvent pas créer deux candidates.
        await ExecuteAsync(connection, tx, "SELECT 1 FROM documents WHERE id = @id FOR UPDATE", ct, ("id", documentId));

        // Une version déjà écartée parce que son texte est celui de la version publiée (fausse nouvelle version CNIL
        // revenue telle quelle) compte comme la version publiée : rien à refaire.
        var status = await ExecuteScalarAsync<string?>(connection, tx, """
            SELECT CASE WHEN v.status = 'discarded' AND v.text_hash = p.text_hash THEN 'published'
                        WHEN v.status = 'discarded' AND v.status_reason LIKE @rejected || '%' THEN 'rejected'
                        ELSE v.status END
            FROM document_versions v
            LEFT JOIN document_versions p ON p.document_id = v.document_id AND p.is_current
            WHERE v.document_id = @document_id AND v.content_hash = @content_hash
            """, ct,
            ("document_id", documentId), ("content_hash", contentHash), ("rejected", VersionStatus.RejectedReason));

        if (status == "rejected")
        {
            await tx.CommitAsync(ct);
            return VersionOutcome.Rejected;
        }

        if (status is not null && VersionStatus.Candidates.Contains(status))
        {
            await tx.CommitAsync(ct);
            return VersionOutcome.Pending;
        }

        if (status == VersionStatus.Quarantined)
        {
            await tx.CommitAsync(ct);
            return VersionOutcome.Quarantined;
        }

        // Une candidate inachevée (panne, contrôle en échec...) est remplacée par ce que la source publie maintenant.
        await ExecuteAsync(connection, tx, """
            UPDATE document_versions SET status = 'discarded', status_reason = @reason
            WHERE document_id = @document_id AND status = ANY (@candidates)
            """, ct,
            ("document_id", documentId), ("candidates", VersionStatus.Candidates),
            ("reason", status == VersionStatus.Published
                ? "la source est revenue à la version publiée"
                : "remplacée par une collecte plus récente"));

        if (status == VersionStatus.Published)
        {
            await tx.CommitAsync(ct);
            return VersionOutcome.Unchanged;
        }

        if (status is not null)
        {
            await ExecuteAsync(connection, tx, """
                UPDATE document_versions SET status = 'collected', status_reason = NULL
                WHERE document_id = @document_id AND content_hash = @content_hash
                """, ct, ("document_id", documentId), ("content_hash", contentHash));
            await tx.CommitAsync(ct);
            return VersionOutcome.Restored;
        }

        await ExecuteAsync(connection, tx, """
            INSERT INTO document_versions (document_id, content_hash, raw_path, status, is_current)
            VALUES (@document_id, @content_hash, @raw_path, 'collected', false)
            """, ct,
            ("document_id", documentId), ("content_hash", contentHash), ("raw_path", rawPath));

        await tx.CommitAsync(ct);
        return VersionOutcome.Created;
    }

    /// <summary>Enregistre l'empreinte du texte extrait (calculée par extract, aussi pour la version publiée).</summary>
    public Task SetTextHashAsync(Guid versionId, string textHash, CancellationToken ct) =>
        ExecuteAsync("UPDATE document_versions SET text_hash = @hash WHERE id = @id AND text_hash IS DISTINCT FROM @hash",
            ct, ("id", versionId), ("hash", textHash));

    /// <summary>
    /// Enregistre le rapport des contrôles qualité. Une candidate découpée passe en « validated » ou en
    /// « quarantined » (status_reason : les contrôles en échec) ; la version publiée garde son statut.
    /// </summary>
    public Task SaveQualityReportAsync(Guid versionId, string reportJson, bool passed, string summary, CancellationToken ct) =>
        ExecuteAsync("""
            UPDATE document_versions
            SET quality_report = @report::jsonb,
                status = CASE WHEN status = 'chunked' THEN @status ELSE status END,
                status_reason = CASE WHEN status = 'chunked' AND NOT @passed THEN @summary ELSE status_reason END
            WHERE id = @id
            """, ct,
            ("id", versionId), ("report", reportJson), ("passed", passed), ("summary", summary),
            ("status", passed ? VersionStatus.Validated : VersionStatus.Quarantined));

    /// <summary>
    /// Décision humaine sur une version en quarantaine, désignée par le début de son empreinte (au moins 8 caractères).
    /// Le motif est gardé dans status_reason. Erreur si l'empreinte est ambiguë ou ne désigne aucune version en quarantaine.
    /// </summary>
    public async Task<VersionChange> DecideQuarantineAsync(
        string hashPrefix, QuarantineDecision decision, string motive, Actor actor, CancellationToken ct)
    {
        if (hashPrefix.Length < 8 || !hashPrefix.All(char.IsAsciiHexDigitLower))
            throw new ArgumentException($"empreinte « {hashPrefix} » : au moins 8 caractères hexadécimaux minuscules (voir la commande versions)");

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        var ids = new List<Guid>();
        await using (var find = new NpgsqlCommand(
            "SELECT id FROM document_versions WHERE status = 'quarantined' AND content_hash LIKE @prefix || '%' FOR UPDATE",
            connection, tx))
        {
            find.Parameters.AddWithValue("prefix", hashPrefix);
            await using var reader = await find.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                ids.Add(reader.GetGuid(0));
        }

        if (ids.Count != 1)
            throw new ArgumentException(ids.Count == 0
                ? $"aucune version en quarantaine dont l'empreinte commence par « {hashPrefix} »"
                : $"{ids.Count} versions en quarantaine commencent par « {hashPrefix} » : préciser l'empreinte");

        VersionChange change;
        try
        {
            await using var update = new NpgsqlCommand($"""
                WITH changed AS (
                    UPDATE document_versions SET status = @status, status_reason = @reason
                    WHERE id = @id RETURNING document_id, content_hash)
                SELECT {Describe.Replace("v.content_hash", "changed.content_hash")}
                JOIN changed ON changed.document_id = d.id
                """, connection, tx);
            update.Parameters.AddWithValue("id", ids[0]);
            var (status, reason) = decision switch
            {
                QuarantineDecision.Release => (VersionStatus.Validated, "quarantaine levée : "),
                QuarantineDecision.Retry => (VersionStatus.Collected, "à recontrôler : "),
                _ => (VersionStatus.Discarded, VersionStatus.RejectedReason),
            };
            update.Parameters.AddWithValue("status", status);
            update.Parameters.AddWithValue("reason", reason + motive);
            await using var reader = await update.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            change = new VersionChange(reader.GetString(0), reader.GetString(1), reader.GetString(2));
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new InvalidOperationException(
                "une autre version de ce document est déjà en cours de traitement : la laisser aboutir, puis recommencer", ex);
        }

        // Le journal dans la même transaction que la décision : une décision sans trace est impossible.
        await AuditStore.WriteAsync(connection, tx, actor, decision.ToString().ToLowerInvariant(), change.ContentHash,
            $"{change.SourceName} / {change.DocumentTitle} — {motive}", ct);

        await tx.CommitAsync(ct);
        return change;
    }

    /// <summary>Détail d'une version (écran de quarantaine du back office), désignée par le début de son empreinte.</summary>
    public async Task<VersionDetail?> GetForReviewAsync(string hashPrefix, CancellationToken ct)
    {
        if (hashPrefix.Length < 8 || !hashPrefix.All(char.IsAsciiHexDigitLower))
            return null;

        await using var command = dataSource.CreateCommand("""
            SELECT v.id, s.name, coalesce(d.title, d.url), d.url, v.content_hash, v.status, v.status_reason,
                   v.fetched_at, v.published_at, v.quality_report::text
            FROM document_versions v
            JOIN documents d ON d.id = v.document_id
            JOIN sources s ON s.id = d.source_id
            WHERE v.content_hash LIKE @prefix || '%'
            LIMIT 2
            """);
        command.Parameters.AddWithValue("prefix", hashPrefix);
        await using var reader = await command.ExecuteReaderAsync(ct);

        VersionDetail? found = null;
        while (await reader.ReadAsync(ct))
        {
            if (found is not null)
                return null; // empreinte ambiguë
            found = new VersionDetail(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7),
                reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8), reader.IsDBNull(9) ? null : reader.GetString(9));
        }

        return found;
    }

    /// <summary>Versions publiées, candidates et en quarantaine, puis les dernières écartées ou rejetées (commande versions).</summary>
    public async Task<IReadOnlyList<VersionRow>> ListForReviewAsync(CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT s.name, coalesce(d.title, d.url), v.content_hash, v.status, v.status_reason, v.fetched_at, v.published_at
            FROM document_versions v
            JOIN documents d ON d.id = v.document_id
            JOIN sources s ON s.id = d.source_id
            WHERE v.status <> 'archived'
              AND (v.status <> 'discarded' OR v.fetched_at > now() - interval '7 days')
            ORDER BY s.name, d.title, v.fetched_at DESC
            """);
        await using var reader = await command.ExecuteReaderAsync(ct);

        var rows = new List<VersionRow>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add(new VersionRow(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetFieldValue<DateTimeOffset>(5),
                reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6)));
        }

        return rows;
    }

    /// <summary>Fait passer une candidate d'une étape à la suivante ; sans effet si elle est déjà plus loin.</summary>
    public Task AdvanceAsync(Guid versionId, string from, string to, CancellationToken ct) =>
        ExecuteAsync("UPDATE document_versions SET status = @to WHERE id = @id AND status = @from",
            ct, ("id", versionId), ("from", from), ("to", to));

    /// <summary>
    /// Écarte les candidates extraites dont le texte est identique à celui de la version publiée : leur fichier brut
    /// diffère (identifiants techniques des pages CNIL) mais rien n'est à découper ni à vectoriser.
    /// </summary>
    public Task<IReadOnlyList<VersionChange>> DiscardIdenticalTextAsync(CancellationToken ct) =>
        QueryChangesAsync($"""
            WITH changed AS (
                UPDATE document_versions c
                SET status = 'discarded', status_reason = 'texte identique à la version publiée'
                FROM document_versions p
                WHERE p.document_id = c.document_id AND p.is_current
                  AND c.status = 'extracted' AND c.text_hash = p.text_hash
                RETURNING c.document_id, c.content_hash)
            SELECT {Describe.Replace("v.content_hash", "changed.content_hash")}
            JOIN changed ON changed.document_id = d.id
            """, ct);

    /// <summary>Candidates dont tous les passages sont vectorisés avec ce modèle : prêtes à publier.</summary>
    public Task<IReadOnlyList<VersionChange>> MarkEmbeddedAsync(string model, CancellationToken ct) =>
        QueryChangesAsync($"""
            WITH changed AS (
                UPDATE document_versions v SET status = 'embedded'
                WHERE v.status = @ready
                  AND EXISTS (SELECT 1 FROM chunks c WHERE c.document_version_id = v.id)
                  AND NOT EXISTS (SELECT 1 FROM chunks c WHERE c.document_version_id = v.id
                                  AND NOT EXISTS (SELECT 1 FROM chunk_embeddings e
                                                  WHERE e.chunk_id = c.id AND e.embedding_model = @model))
                RETURNING v.document_id, v.content_hash)
            SELECT {Describe.Replace("v.content_hash", "changed.content_hash")}
            JOIN changed ON changed.document_id = d.id
            """, ct, ("ready", VersionStatus.ReadyToEmbed), ("model", model));

    /// <summary>
    /// Publie les candidates prêtes, toutes dans une transaction : pour chaque document, l'ancienne version passe en
    /// « archived » et la candidate en « published » (is_current). La recherche passe d'une version à l'autre sans
    /// jamais rester sans passages.
    /// </summary>
    public async Task<IReadOnlyList<VersionChange>> PublishAsync(CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        // L'ancienne version d'abord : l'index unique partiel one_current_version_per_document l'impose.
        await using (var archive = new NpgsqlCommand("""
            UPDATE document_versions p SET status = 'archived', is_current = false
            WHERE p.is_current
              AND EXISTS (SELECT 1 FROM document_versions c WHERE c.document_id = p.document_id AND c.status = 'embedded')
            """, connection, tx))
            await archive.ExecuteNonQueryAsync(ct);

        var changes = new List<VersionChange>();
        await using (var publish = new NpgsqlCommand($"""
            WITH published AS (
                UPDATE document_versions
                SET status = 'published', is_current = true, published_at = now(), status_reason = NULL
                WHERE status = 'embedded'
                RETURNING document_id, content_hash)
            SELECT {Describe.Replace("v.content_hash", "published.content_hash")}
            JOIN published ON published.document_id = d.id
            """, connection, tx))
        await using (var reader = await publish.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                changes.Add(new VersionChange(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }

        await tx.CommitAsync(ct);
        return changes;
    }

    private async Task<IReadOnlyList<VersionChange>> QueryChangesAsync(
        string sql, CancellationToken ct, params (string Name, object Value)[] parameters)
    {
        await using var command = dataSource.CreateCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await using var reader = await command.ExecuteReaderAsync(ct);

        var changes = new List<VersionChange>();
        while (await reader.ReadAsync(ct))
            changes.Add(new VersionChange(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        return changes;
    }

    private async Task ExecuteAsync(string sql, CancellationToken ct, params (string Name, object Value)[] parameters)
    {
        await using var command = dataSource.CreateCommand(sql);
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync(ct);
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

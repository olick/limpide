using Limpide.Core.Answering;
using Npgsql;
using NpgsqlTypes;

namespace Limpide.Infrastructure.Storage;

/// <summary>Journal des réponses (table answer_log, migration 0006_journal_des_reponses.sql, ADR-018).</summary>
public sealed class AnswerLogStore(NpgsqlDataSource dataSource, EmbeddingOptions embedding)
{
    /// <summary>
    /// Enregistre une ligne, avec le texte de la question s'il est conservé (question_log, ADR-019), et supprime ce qui
    /// a dépassé sa durée de conservation (12 mois pour le journal, 30 jours pour le texte) : la purge ne dépend
    /// d'aucune tâche planifiée.
    /// </summary>
    public async Task SaveAsync(AnswerLogEntry entry, KeptQuestion? question, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        await using (var purge = new NpgsqlCommand("DELETE FROM answer_log WHERE at < now() - @retention", connection, tx))
        {
            purge.Parameters.AddWithValue("retention", AnswerLogEntry.Retention);
            await purge.ExecuteNonQueryAsync(ct);
        }

        // À chaque question, conservée ou non : sinon, une démo où tous s'opposent garderait les anciennes.
        await using (var purge = new NpgsqlCommand("DELETE FROM question_log WHERE at < now() - @retention", connection, tx))
        {
            purge.Parameters.AddWithValue("retention", KeptQuestion.Retention);
            await purge.ExecuteNonQueryAsync(ct);
        }

        long id;
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO answer_log (outcome, trace_id, question_length, search_strategy, embedding_model, model,
                prompt_version, passages, cited, best_score, embedding_ms, search_ms, generation_ms, total_ms,
                input_tokens, output_tokens, cost_eur, guardrails, error)
            VALUES (@outcome, @trace_id, @question_length, @search_strategy, @embedding_model, @model,
                @prompt_version, @passages, @cited, @best_score, @embedding_ms, @search_ms, @generation_ms, @total_ms,
                @input_tokens, @output_tokens, @cost_eur, @guardrails, @error)
            RETURNING id
            """, connection, tx))
        {
            var p = insert.Parameters;
            p.AddWithValue("outcome", entry.Outcome);
            p.Add(Nullable("trace_id", NpgsqlDbType.Text, entry.TraceId));
            p.AddWithValue("question_length", entry.QuestionLength);
            p.Add(Nullable("search_strategy", NpgsqlDbType.Text, entry.SearchStrategy));
            // Pas de recherche (quota, erreur précoce) : pas de modèle d'embedding en jeu.
            p.Add(Nullable("embedding_model", NpgsqlDbType.Text, entry.SearchStrategy is null ? null : embedding.Label));
            p.Add(Nullable("model", NpgsqlDbType.Text, entry.Model));
            p.Add(Nullable("prompt_version", NpgsqlDbType.Text, entry.PromptVersion));
            p.AddWithValue("passages", entry.Passages);
            p.AddWithValue("cited", entry.Cited);
            p.Add(Nullable("best_score", NpgsqlDbType.Double, entry.BestScore));
            p.Add(Nullable("embedding_ms", NpgsqlDbType.Integer, Ms(entry.Embedding)));
            p.Add(Nullable("search_ms", NpgsqlDbType.Integer, Ms(entry.Search)));
            p.Add(Nullable("generation_ms", NpgsqlDbType.Integer, Ms(entry.Generation)));
            p.Add(Nullable("total_ms", NpgsqlDbType.Integer, Ms(entry.Total)));
            p.Add(Nullable("input_tokens", NpgsqlDbType.Bigint, entry.InputTokens));
            p.Add(Nullable("output_tokens", NpgsqlDbType.Bigint, entry.OutputTokens));
            p.Add(Nullable("cost_eur", NpgsqlDbType.Numeric, entry.CostEur));
            p.Add(new NpgsqlParameter("guardrails", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = entry.Guardrails.ToArray() });
            p.Add(Nullable("error", NpgsqlDbType.Text, entry.Error));
            id = (long)(await insert.ExecuteScalarAsync(ct))!;
        }

        if (question is not null)
        {
            await using var insert = new NpgsqlCommand(
                "INSERT INTO question_log (answer_log_id, question, masked) VALUES (@id, @question, @masked)", connection, tx);
            insert.Parameters.AddWithValue("id", id);
            insert.Parameters.AddWithValue("question", question.Text);
            insert.Parameters.Add(new NpgsqlParameter("masked", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = question.Masked.ToArray() });
            await insert.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    private static int? Ms(TimeSpan? duration) => duration is { } d ? (int)Math.Round(d.TotalMilliseconds) : null;

    private static NpgsqlParameter Nullable(string name, NpgsqlDbType type, object? value) =>
        new(name, type) { Value = value ?? DBNull.Value };
}

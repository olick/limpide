using Limpide.Core.Administration;
using Npgsql;

namespace Limpide.Infrastructure.Storage;

/// <summary>Une question conservée, avec ce que le journal des réponses dit de son issue (sans la réponse).</summary>
public sealed record StoredQuestion(
    long Id, DateTimeOffset At, string Question, IReadOnlyList<string> Masked, string Outcome, double? BestScore,
    string? TraceId);

/// <summary>
/// Lecture des questions conservées par le back office (table question_log, migration 0009, ADR-019). L'écriture et
/// la purge (30 jours) sont dans <see cref="AnswerLogStore"/>, dans la transaction de la ligne du journal.
/// </summary>
public sealed class QuestionLogStore(NpgsqlDataSource dataSource)
{
    /// <summary>Les plus récentes d'abord ; seulement celles encore dans leur durée de conservation.</summary>
    public async Task<IReadOnlyList<StoredQuestion>> ListAsync(int limit, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT q.answer_log_id, q.at, q.question, q.masked, a.outcome, a.best_score, a.trace_id
            FROM question_log q JOIN answer_log a ON a.id = q.answer_log_id
            WHERE q.at >= now() - @retention
            ORDER BY q.at DESC LIMIT @limit
            """);
        command.Parameters.AddWithValue("retention", Core.Answering.KeptQuestion.Retention);
        command.Parameters.AddWithValue("limit", limit);
        await using var reader = await command.ExecuteReaderAsync(ct);

        var list = new List<StoredQuestion>();
        while (await reader.ReadAsync(ct))
            list.Add(new StoredQuestion(reader.GetInt64(0), reader.GetFieldValue<DateTimeOffset>(1), reader.GetString(2),
                reader.GetFieldValue<string[]>(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetDouble(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        return list;
    }

    /// <summary>
    /// Supprime le texte d'une question (demande d'effacement d'un visiteur, ou contenu qui n'aurait pas dû être
    /// conservé) et le note au journal, sans le texte ; la ligne d'answer_log, sans texte, reste.
    /// </summary>
    public async Task DeleteAsync(long id, Actor actor, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await using (var delete = new NpgsqlCommand("DELETE FROM question_log WHERE answer_log_id = @id", connection, tx))
        {
            delete.Parameters.AddWithValue("id", id);
            if (await delete.ExecuteNonQueryAsync(ct) == 0)
                return; // déjà supprimée, ou purgée
        }

        await AuditStore.WriteAsync(connection, tx, actor, "question-deleted", id.ToString(), null, ct);
        await tx.CommitAsync(ct);
    }
}

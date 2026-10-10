using Limpide.Core.Feedback;
using Npgsql;
using NpgsqlTypes;

namespace Limpide.Infrastructure.Storage;

public sealed record StoredFeedback(DateTimeOffset CreatedAt, VisitorFeedback Feedback);

/// <summary>Retours des visiteurs (table feedback, migration 0002_feedback.sql).</summary>
public sealed class FeedbackStore(NpgsqlDataSource dataSource)
{
    /// <summary>
    /// Enregistre un retour et supprime ceux qui ont dépassé la durée de conservation annoncée (12 mois) :
    /// la purge ne dépend d'aucune tâche planifiée.
    /// </summary>
    public async Task SaveAsync(VisitorFeedback feedback, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        await using (var purge = new NpgsqlCommand("DELETE FROM feedback WHERE created_at < now() - @retention", connection, tx))
        {
            purge.Parameters.AddWithValue("retention", VisitorFeedback.Retention);
            await purge.ExecuteNonQueryAsync(ct);
        }

        await using (var insert = new NpgsqlCommand("""
            INSERT INTO feedback (understood, misleading, under_the_hood, question, answer, model, prompt_version, cited)
            VALUES (@understood, @misleading, @under_the_hood, @question, @answer, @model, @prompt_version, @cited)
            """, connection, tx))
        {
            var context = feedback.Context;
            insert.Parameters.Add(Text("understood", feedback.Understood));
            insert.Parameters.Add(Text("misleading", feedback.Misleading));
            insert.Parameters.Add(Text("under_the_hood", feedback.UnderTheHood));
            insert.Parameters.Add(Text("question", context?.Question));
            insert.Parameters.Add(Text("answer", context?.Answer));
            insert.Parameters.Add(Text("model", context?.Model));
            insert.Parameters.Add(Text("prompt_version", context?.PromptVersion));
            insert.Parameters.Add(new NpgsqlParameter("cited", NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                Value = context is null ? DBNull.Value : context.Cited.ToArray(),
            });
            await insert.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    /// <summary>Les retours les plus récents d'abord.</summary>
    public async Task<IReadOnlyList<StoredFeedback>> ListAsync(int limit, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT created_at, understood, misleading, under_the_hood, question, answer, model, prompt_version, cited
            FROM feedback ORDER BY created_at DESC LIMIT @limit
            """);
        command.Parameters.AddWithValue("limit", limit);
        await using var reader = await command.ExecuteReaderAsync(ct);

        var list = new List<StoredFeedback>();
        while (await reader.ReadAsync(ct))
        {
            string? Get(int i) => reader.IsDBNull(i) ? null : reader.GetString(i);
            var context = Get(4) is { } question
                ? new AnsweredQuestion(question, Get(5) ?? "", Get(6) ?? "", Get(7) ?? "",
                    reader.IsDBNull(8) ? [] : reader.GetFieldValue<string[]>(8))
                : null;
            list.Add(new StoredFeedback(reader.GetFieldValue<DateTimeOffset>(0), new VisitorFeedback(Get(1), Get(2), Get(3), context)));
        }

        return list;
    }

    private static NpgsqlParameter Text(string name, string? value) =>
        new(name, NpgsqlDbType.Text) { Value = (object?)value ?? DBNull.Value };
}

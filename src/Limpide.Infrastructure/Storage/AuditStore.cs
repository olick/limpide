using Limpide.Core.Administration;
using Npgsql;

namespace Limpide.Infrastructure.Storage;

public sealed record AdminAction(DateTimeOffset At, string Actor, string? ActorName, string Action, string Target, string? Detail);

/// <summary>
/// Journal des décisions humaines (table admin_actions, migration 0004) : quarantaines levées, recontrôlées ou rejetées,
/// retours traités. Écrit par les stores dans la transaction de la décision ; jamais modifié ni purgé.
/// </summary>
public sealed class AuditStore(NpgsqlDataSource dataSource)
{
    public static async Task WriteAsync(
        NpgsqlConnection connection, NpgsqlTransaction tx, Actor actor, string action, string target, string? detail, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO admin_actions (actor, actor_name, action, target, detail)
            VALUES (@actor, @actor_name, @action, @target, @detail)
            """, connection, tx);
        command.Parameters.AddWithValue("actor", actor.Id);
        command.Parameters.AddWithValue("actor_name", actor.Name);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("target", target);
        command.Parameters.AddWithValue("detail", (object?)detail ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Les décisions les plus récentes d'abord.</summary>
    public async Task<IReadOnlyList<AdminAction>> ListAsync(int limit, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT at, actor, actor_name, action, target, detail FROM admin_actions ORDER BY at DESC LIMIT @limit");
        command.Parameters.AddWithValue("limit", limit);
        await using var reader = await command.ExecuteReaderAsync(ct);

        var actions = new List<AdminAction>();
        while (await reader.ReadAsync(ct))
        {
            actions.Add(new AdminAction(reader.GetFieldValue<DateTimeOffset>(0), reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3), reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return actions;
    }
}

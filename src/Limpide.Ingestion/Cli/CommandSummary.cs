using System.Text.Json;

namespace Limpide.Ingestion.Cli;

/// <summary>
/// Compteurs d'une commande d'ingestion (documents inchangés, nouvelles versions, échecs...), écrits en JSON sur la
/// dernière ligne de la sortie : l'opérateur Docker d'Airflow garde cette ligne comme résultat de la tâche (XCom).
/// </summary>
public sealed class CommandSummary(string command)
{
    private readonly OrderedDictionary<string, int> counts = [];

    public bool IsEmpty => counts.Count == 0;

    public void Add(string key, int count = 1) =>
        counts[key] = counts.GetValueOrDefault(key) + count;

    public string ToJson(int exitCode)
    {
        var fields = new OrderedDictionary<string, object> { ["command"] = command, ["exitCode"] = exitCode };
        foreach (var (key, count) in counts)
            fields[key] = count;
        return JsonSerializer.Serialize(fields);
    }
}

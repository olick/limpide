using System.Text.Json;

namespace Limpide.Ingestion.Cli;

/// <summary>
/// Compteurs d'une commande d'ingestion (documents inchangés, nouvelles versions, échecs...), écrits en JSON sur la
/// dernière ligne de la sortie : l'opérateur Docker d'Airflow garde cette ligne comme résultat de la tâche (XCom).
/// </summary>
public sealed class CommandSummary(string command)
{
    private readonly OrderedDictionary<string, int> counts = [];
    private readonly OrderedDictionary<string, List<string>> details = [];
    private readonly OrderedDictionary<string, decimal?> measures = [];

    public bool IsEmpty => counts.Count == 0 && details.Count == 0 && measures.Count == 0;

    public void Add(string key, int count = 1) =>
        counts[key] = counts.GetValueOrDefault(key) + count;

    /// <summary>Mesure non entière (ex. un coût en euros) ; null : inconnue, écrite telle quelle.</summary>
    public void Measure(string key, decimal? value) => measures[key] = value;

    /// <summary>Précision à reprendre dans l'alerte (ex. la version mise en quarantaine et pourquoi).</summary>
    public void Detail(string key, string value)
    {
        if (!details.TryGetValue(key, out var values))
            details[key] = values = [];
        values.Add(value);
    }

    public string ToJson(int exitCode)
    {
        var fields = new OrderedDictionary<string, object> { ["command"] = command, ["exitCode"] = exitCode };
        foreach (var (key, count) in counts)
            fields[key] = count;
        foreach (var (key, value) in measures)
            fields[key] = value!;
        foreach (var (key, values) in details)
            fields[key] = values;
        // Accents lisibles dans les logs et l'alerte (pas de \u00e9).
        return JsonSerializer.Serialize(fields, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }
}

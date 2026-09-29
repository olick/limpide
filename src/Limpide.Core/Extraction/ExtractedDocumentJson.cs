using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Limpide.Core.Extraction;

/// <summary>Format des fichiers data/extracted : JSON indenté, accents lisibles, pour pouvoir les relire à l'œil.</summary>
public static class ExtractedDocumentJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
        RespectRequiredConstructorParameters = true,
    };

    public static string Serialize(ExtractedDocument document) => JsonSerializer.Serialize(document, Options);

    public static ExtractedDocument Deserialize(string json) =>
        JsonSerializer.Deserialize<ExtractedDocument>(json, Options)
        ?? throw new InvalidDataException("fichier extrait vide");
}

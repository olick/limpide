namespace Limpide.Ingestion;

/// <summary>Section « Ingestion » de la configuration. Chemins relatifs au répertoire courant.</summary>
public sealed class IngestionOptions
{
    public string CorpusPath { get; set; } = "corpus.json";

    public string QuestionsPath { get; set; } = "eval/questions.json";

    /// <summary>Racine des fichiers bruts. En base, <c>raw_path</c> est relatif à cette racine.</summary>
    public string RawDataPath { get; set; } = "data/raw";

    /// <summary>Racine des textes extraits : même arborescence et même nom que le fichier brut, en .json.</summary>
    public string ExtractedDataPath { get; set; } = "data/extracted";

    public string UserAgent { get; set; } = "Limpide/0.1 (+https://github.com/olick/limpide)";

    public TimeSpan DelayBetweenRequests { get; set; } = TimeSpan.FromSeconds(1.5);

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(60);
}

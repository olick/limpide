namespace Limpide.Ingestion;

/// <summary>Section « Ingestion » de la configuration. Chemins relatifs au répertoire courant.</summary>
public sealed class IngestionOptions
{
    public string CorpusPath { get; set; } = "corpus.json";

    public string QuestionsPath { get; set; } = "eval/questions.json";

    /// <summary>Rapports de evaluate-answers (Markdown, un par jour et par modèle), versionnés avec le code.</summary>
    public string ResultsPath { get; set; } = "eval/results";

    /// <summary>Réponses déjà relues, avec leur verdict, pour mesurer le juge de fidélité (judge-calibrate).</summary>
    public string JudgeCalibrationPath { get; set; } = "eval/calibration/fidelite.json";

    /// <summary>evaluate-answers fait relire chaque réponse par le juge de fidélité (≈ 0,13 $ de plus pour 50 questions).</summary>
    public bool JudgeAnswers { get; set; } = true;

    /// <summary>Racine des fichiers bruts. En base, <c>raw_path</c> est relatif à cette racine.</summary>
    public string RawDataPath { get; set; } = "data/raw";

    /// <summary>Racine des textes extraits : même arborescence et même nom que le fichier brut, en .json.</summary>
    public string ExtractedDataPath { get; set; } = "data/extracted";

    public string UserAgent { get; set; } = "Limpide/0.1 (+https://github.com/olick/limpide)";

    public TimeSpan DelayBetweenRequests { get; set; } = TimeSpan.FromSeconds(1.5);

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// « Structural » (par défaut : article, considérant, section) ou « FixedSize » (≈ 1 000 caractères, chevauchement de
    /// 200) : pour mesurer l'écart, sur une copie de la base (ADR sur la stratégie de découpage).
    /// </summary>
    public string ChunkingStrategy { get; set; } = "Structural";
}

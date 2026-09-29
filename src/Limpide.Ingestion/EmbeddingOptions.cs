namespace Limpide.Ingestion;

/// <summary>Section « Embedding » de la configuration : fournisseur d'embeddings (Ollama en local).</summary>
public sealed class EmbeddingOptions
{
    public Uri Endpoint { get; set; } = new("http://localhost:11434");

    /// <summary>Nom du modèle, enregistré sur chaque passage (<c>chunks.embedding_model</c>).</summary>
    public string Model { get; set; } = "bge-m3";

    /// <summary>Doit correspondre à la colonne <c>chunks.embedding vector(1024)</c>.</summary>
    public int Dimensions { get; set; } = 1024;

    /// <summary>Passages envoyés par appel ; chaque lot est enregistré dès qu'il est calculé.</summary>
    public int BatchSize { get; set; } = 16;
}

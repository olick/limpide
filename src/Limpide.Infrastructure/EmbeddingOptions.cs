namespace Limpide.Infrastructure;

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

    /// <summary>
    /// Vectoriser « titre de rattachement + texte » plutôt que le texte seul. Le texte stocké et affiché ne change pas.
    /// Retenu en semaine 1 : 8/10 dans le top 5 contre 5/10 (docs/notes/observations-decoupage.md).
    /// </summary>
    public bool IncludeHeading { get; set; } = true;

    /// <summary>
    /// Ce qui a produit un vecteur (modèle et texte vectorisé), enregistré dans <c>chunks.embedding_model</c> :
    /// deux réglages différents ne se mélangent jamais, et changer de réglage relance le calcul.
    /// </summary>
    public string Label => IncludeHeading ? $"{Model}+titre" : Model;

    public string TextToEmbed(string heading, string content) => IncludeHeading ? $"{heading}\n{content}" : content;
}

namespace Limpide.Infrastructure;

/// <summary>Section « Embedding » de la configuration : fournisseur d'embeddings (Ollama en local).</summary>
public sealed class EmbeddingOptions
{
    public const string Ollama = "Ollama";
    public const string OpenAI = "OpenAI";

    /// <summary>
    /// <c>Ollama</c> (défaut du code : bge-m3 sur la machine, hors ligne) ou <c>OpenAI</c> : toute API d'embeddings
    /// compatible OpenAI, à l'adresse <see cref="Endpoint"/>, avec la clé <see cref="ApiKey"/>. La configuration
    /// partagée choisit OVHcloud AI Endpoints et Qwen3-Embedding-8B (ADR-012).
    /// Mesures de la S6 : docs/notes/comparatif-couts-hebergement.md.
    /// </summary>
    public string Provider { get; set; } = Ollama;

    /// <summary>Adresse d'Ollama, ou adresse de base de l'API (ex. https://oai.endpoints.kepler.ai.cloud.ovh.net/v1).</summary>
    public Uri Endpoint { get; set; } = new("http://localhost:11434");

    /// <summary>Clé de l'API (fournisseur <c>OpenAI</c>) : user-secrets ou coffre, jamais dans un fichier versionné.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Nom du modèle, enregistré avec chaque vecteur (<c>chunk_embeddings.embedding_model</c>, via <see cref="Label"/>).</summary>
    public string Model { get; set; } = "bge-m3";

    /// <summary>Doit correspondre à la colonne <c>chunk_embeddings.embedding vector(1024)</c>.</summary>
    public int Dimensions { get; set; } = 1024;

    /// <summary>
    /// Demander <see cref="Dimensions"/> à l'API (fournisseur <c>OpenAI</c>) : pour les modèles à taille réglable
    /// (Qwen3-Embedding : 4 096 par défaut, réduit à 1 024). Inutile pour bge-m3, toujours en 1 024.
    /// </summary>
    public bool RequestDimensions { get; set; }

    /// <summary>
    /// Consigne placée devant la question seulement (jamais devant les passages), pour les modèles qui l'attendent
    /// (Qwen3-Embedding : « Instruct: …\nQuery: »). Vide : la question telle quelle (bge-m3).
    /// </summary>
    public string? QueryInstruction { get; set; }

    public string QueryText(string question) =>
        QueryInstruction is { Length: > 0 } instruction ? $"{instruction}{question}" : question;

    /// <summary>
    /// Tarif par modèle, en euros par million de tokens (coût d'une ingestion, S8). Modèle absent : coût « inconnu »,
    /// jamais deviné ; 0 pour un modèle local.
    /// </summary>
    public Dictionary<string, decimal> PricesPerMillion { get; set; } = [];

    /// <summary>Coût en euros de <paramref name="tokens"/> tokens ; null si le tarif ou le nombre de tokens manque.</summary>
    public decimal? Cost(long? tokens) =>
        PricesPerMillion.TryGetValue(Model, out var price)
            ? price == 0 ? 0m : tokens * price / 1_000_000m
            : null;

    /// <summary>Passages envoyés par appel ; chaque lot est enregistré dès qu'il est calculé.</summary>
    public int BatchSize { get; set; } = 16;

    /// <summary>
    /// Vectoriser « titre de rattachement + texte » plutôt que le texte seul. Le texte stocké et affiché ne change pas.
    /// Retenu en semaine 1 : 8/10 dans le top 5 contre 5/10 (docs/notes/observations-decoupage.md).
    /// </summary>
    public bool IncludeHeading { get; set; } = true;

    /// <summary>
    /// Ce qui a produit un vecteur (modèle et texte vectorisé), enregistré dans <c>chunk_embeddings.embedding_model</c> :
    /// deux réglages différents ne se mélangent jamais ; un nouveau réglage a ses propres vecteurs, calculés par embed
    /// à côté des autres (ADR-017).
    /// </summary>
    public string Label => IncludeHeading ? $"{Model}+titre" : Model;

    public string TextToEmbed(string heading, string content) => IncludeHeading ? $"{heading}\n{content}" : content;
}

using Limpide.Core.Answering;

namespace Limpide.Infrastructure;

/// <summary>
/// Section « Generation » de la configuration : LLM qui rédige les réponses (Mistral, inférence UE, ADR-004).
/// La clé d'API se lit à part, sous « Mistral:ApiKey » (user-secrets en local, Key Vault ensuite).
/// </summary>
public sealed class GenerationOptions
{
    /// <summary>API compatible OpenAI. Point d'accès UE : les questions sont traitées en Europe.</summary>
    public Uri Endpoint { get; set; } = new("https://api.eu.mistral.ai/v1");

    /// <summary>Version datée, jamais un alias « -latest » qui changerait de modèle sans prévenir.</summary>
    public string Model { get; set; } = "mistral-medium-2604";

    public int PassageCount { get; set; } = 5;

    /// <summary>0 : réponses aussi reproductibles que possible, pour comparer les mesures entre elles.</summary>
    public float Temperature { get; set; }

    public int MaxOutputTokens { get; set; } = 1000;

    /// <summary>Seuil de pertinence (similarité cosinus du meilleur passage) ; 0 = désactivé. Calibré dans l'ADR-005.</summary>
    public double MinScore { get; set; }

    /// <summary>Tarif par modèle, en dollars par million de tokens, majoration UE comprise. Relevés dans l'ADR-004.</summary>
    public Dictionary<string, ModelPrice> Prices { get; set; } = [];

    public GenerationSettings ToSettings() =>
        new(Model, Prices.GetValueOrDefault(Model), PassageCount, Temperature, MaxOutputTokens, MinScore);
}

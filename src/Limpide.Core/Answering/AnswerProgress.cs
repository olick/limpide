using Limpide.Core.Search;

namespace Limpide.Core.Answering;

/// <summary>
/// Point d'étape publié pendant le traitement d'une question (radiographie en direct, ADR-022) : la recherche vient
/// de se terminer. Rien n'est simulé : ce sont les passages et les durées que la réponse finale reprendra.
/// </summary>
/// <param name="AboveThreshold">Le meilleur passage atteint le seuil : le modèle va rédiger ; sinon, refus sans modèle.</param>
public sealed record AnswerProgress(
    IReadOnlyList<RetrievedPassage> Passages,
    TimeSpan EmbeddingDuration,
    TimeSpan QueryDuration,
    bool AboveThreshold);

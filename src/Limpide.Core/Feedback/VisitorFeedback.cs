using Limpide.Core.Answering;

namespace Limpide.Core.Feedback;

/// <summary>
/// Retour d'un visiteur (formulaire en bas de page). Aucune adresse IP ni identifiant. La dernière question et
/// sa réponse ne sont jointes qu'avec l'accord explicite du visiteur.
/// </summary>
public sealed record VisitorFeedback(
    string? Understood,
    string? Misleading,
    string? UnderTheHood,
    AnsweredQuestion? Context = null)
{
    public const int MaxLength = 1000;

    /// <summary>Durée de conservation annoncée au visiteur ; les retours plus anciens sont supprimés.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(365);

    /// <summary>
    /// Construit un retour à partir du formulaire : espaces retirés, réponses vides ignorées.
    /// Null si aucune des trois questions n'a reçu de réponse.
    /// </summary>
    public static VisitorFeedback? From(string? understood, string? misleading, string? underTheHood, AnswerResult? attach)
    {
        var feedback = new VisitorFeedback(
            Clean(understood), Clean(misleading), Clean(underTheHood),
            attach is null ? null : AnsweredQuestion.From(attach));

        return feedback.Understood is null && feedback.Misleading is null && feedback.UnderTheHood is null
            ? null
            : feedback;
    }

    private static string? Clean(string? text)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        return trimmed.Length <= MaxLength ? trimmed : trimmed[..MaxLength];
    }
}

/// <summary>La question jointe au retour, avec ce qui a produit la réponse (modèle, consignes, passages cités).</summary>
public sealed record AnsweredQuestion(string Question, string Answer, string Model, string PromptVersion, IReadOnlyList<string> Cited)
{
    public static AnsweredQuestion From(AnswerResult result) => new(
        result.Question,
        result.Answer,
        result.Model,
        result.PromptVersion,
        result.CitedPassages.Select(p => p.Passage.Anchor is null ? p.Passage.Heading : $"{p.Passage.Heading} ({p.Passage.Anchor})").ToList());
}

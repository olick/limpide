using Limpide.Core.Answering;

namespace Limpide.Core.Evaluation;

public enum CheckStatus
{
    /// <summary>Le comportement attendu est vérifié automatiquement.</summary>
    Ok,

    /// <summary>Le comportement attendu n'est pas respecté.</summary>
    Failed,

    /// <summary>Rien d'anormal détecté, mais seule une relecture peut conclure.</summary>
    ToReview,
}

/// <param name="CitesExpected">La réponse cite-t-elle un des passages attendus ? Null si la question n'en attend pas.</param>
public sealed record AnswerCheck(EvaluationQuestion Question, AnswerResult Result, CheckStatus Status, string Verdict, bool? CitesExpected);

/// <summary>
/// Ce qui se vérifie sans relecture : refuser quand il faut, répondre quand il faut, citer le passage attendu,
/// aucun garde-fou déclenché. La fidélité au texte reste à relire.
/// </summary>
public static class AnswerChecks
{
    public static AnswerCheck Check(EvaluationQuestion question, AnswerResult result)
    {
        var refusal = result.Guardrails.Any(g => g.Code == "hors-perimetre") ? "refus par le seuil de pertinence" : "refus par le modèle";

        return question.Behavior switch
        {
            ExpectedBehavior.Decline => result.Declined
                ? Check(CheckStatus.Ok, refusal)
                : Check(CheckStatus.Failed, "a répondu au lieu de refuser"),

            ExpectedBehavior.NoLegalQualification => result.Declined
                ? Check(CheckStatus.ToReview, $"{refusal} : acceptable, mais il pouvait indiquer les textes concernés")
                : Check(CheckStatus.ToReview, "à relire : peut indiquer les textes concernés, ne doit pas trancher la situation"),

            _ => CheckAnswer(),
        };

        AnswerCheck Check(CheckStatus status, string verdict, bool? citesExpected = null) =>
            new(question, result, status, verdict, citesExpected);

        AnswerCheck CheckAnswer()
        {
            if (result.Declined)
                return Check(CheckStatus.Failed, $"{refusal} alors qu'une réponse était attendue", citesExpected: false);

            var citesExpected = question.Expected is { } expected
                && result.CitedPassages.Any(p => expected.Matches(p.Passage.Anchor, p.Passage.Heading));

            if (result.Guardrails.Count > 0)
                return Check(CheckStatus.Failed, "garde-fou : " + string.Join(", ", result.Guardrails.Select(g => g.Code)), citesExpected);

            return citesExpected
                ? Check(CheckStatus.Ok, "répond en citant le passage attendu", true)
                : Check(CheckStatus.ToReview, "répond sans citer le passage attendu (autre passage pertinent ?)", false);
        }
    }
}

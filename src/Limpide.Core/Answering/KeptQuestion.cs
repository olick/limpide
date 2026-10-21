using Limpide.Core.Protection;

namespace Limpide.Core.Answering;

/// <summary>
/// Texte d'une question posée à la démo, conservé pour améliorer Limpide (table question_log, ADR-019) : 30 jours,
/// sauf si le visiteur s'y oppose, après masquage des données personnelles évidentes. Il ne va jamais dans les traces
/// ni dans les messages de la supervision (ADR-018) : seulement dans cette table, lisible par le back office.
/// </summary>
/// <param name="Masked">Types de données masquées (<see cref="PersonalDataMask"/>).</param>
public sealed record KeptQuestion(string Text, IReadOnlyList<string> Masked)
{
    /// <summary>Durée annoncée dans la mention d'information ; les questions plus anciennes sont supprimées.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    public static KeptQuestion From(string question)
    {
        var masked = PersonalDataMask.Mask(question.Trim());
        return new KeptQuestion(masked.Text, masked.Masked);
    }
}

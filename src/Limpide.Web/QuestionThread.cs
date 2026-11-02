using Limpide.Core.Answering;

namespace Limpide.Web;

/// <summary>Une question du fil : en attente tant que sa réponse n'est pas arrivée.</summary>
public sealed class ThreadEntry
{
    /// <summary>« q1 », « q2 »… : préfixe des identifiants de la page (titre, extraits cités).</summary>
    public required string Id { get; init; }

    public required string Question { get; init; }

    public AnswerResult? Result { get; set; }

    /// <summary>Réponse en HTML (<see cref="AnswerMarkdown"/>), calculée une fois à l'arrivée.</summary>
    public string AnswerHtml { get; set; } = "";

    /// <summary>Couche « Au fond » dépliée.</summary>
    public bool DetailOpen { get; set; }

    public bool Pending => Result is null;
}

/// <summary>
/// Fil des questions de la visite (ADR-022) : propre au circuit Blazor (service « scoped »), il survit au passage par
/// les autres pages et disparaît avec la visite. Rien n'en est envoyé au modèle : chaque question reste traitée seule.
/// </summary>
public sealed class QuestionThread
{
    private readonly List<ThreadEntry> entries = [];
    private int next;

    public IReadOnlyList<ThreadEntry> Entries => entries;

    public ThreadEntry Add(string question)
    {
        var entry = new ThreadEntry { Id = $"q{++next}", Question = question };
        entries.Add(entry);
        return entry;
    }

    public void Remove(ThreadEntry entry) => entries.Remove(entry);

    public void Clear() => entries.Clear();
}

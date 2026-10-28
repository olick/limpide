using System.Security.Cryptography;
using Limpide.Core.Answering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Limpide.Web;

/// <summary>
/// Une visite = une page ouverte (un circuit Blazor). Son identifiant, tiré au hasard, relie les messages d'une même
/// visite dans les journaux (ADR-018) ; il n'est lié ni à l'adresse IP ni à un cookie, et meurt avec la page.
/// </summary>
public sealed class Visit
{
    public string Id { get; } = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4));

    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastActivity { get; private set; } = DateTimeOffset.UtcNow;

    public int Questions { get; private set; }

    public int Pages { get; private set; }

    public void QuestionAsked() { Questions++; LastActivity = DateTimeOffset.UtcNow; }

    public void PageViewed() { Pages++; LastActivity = DateTimeOffset.UtcNow; }

    /// <summary>Ajoute « visite » aux messages écrits pendant la portée (attribut filtrable dans Loki).</summary>
    public IDisposable? Scope(ILogger logger) => logger.BeginScope(new Dictionary<string, object> { ["visite"] = Id });
}

/// <summary>Début, pages vues et fin de chaque visite, dans les journaux.</summary>
public sealed class VisitCircuitHandler(Visit visit, NavigationManager navigation, ILogger<VisitCircuitHandler> logger) : CircuitHandler
{
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken ct)
    {
        using (visit.Scope(logger))
            logger.VisitOpened();
        PageViewed(navigation.Uri);
        navigation.LocationChanged += OnLocationChanged;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Appelé quand Blazor libère le circuit, quelques minutes après la fermeture de la page : la durée retenue est
    /// celle de l'activité (jusqu'à la dernière page ou question), pas ce délai.
    /// </summary>
    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken ct)
    {
        navigation.LocationChanged -= OnLocationChanged;
        using (visit.Scope(logger))
            logger.VisitClosed(visit.Pages, visit.Questions, (int)(visit.LastActivity - visit.StartedAt).TotalSeconds);
        return Task.CompletedTask;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => PageViewed(e.Location);

    private void PageViewed(string uri)
    {
        visit.PageViewed();
        // Chemin seulement : ni paramètres ni fragment.
        var path = "/" + navigation.ToBaseRelativePath(uri).Split('?', '#')[0];
        using (visit.Scope(logger))
            logger.PageViewed(path);
    }
}

/// <summary>
/// Messages des opérations d'un visiteur (ADR-018) : des mesures et des codes, jamais le texte d'une question ni
/// d'une réponse. Le détail étape par étape est dans la trace (même trace_id).
/// </summary>
public static partial class VisitorLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Visite ouverte")]
    public static partial void VisitOpened(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Page {Page}")]
    public static partial void PageViewed(this ILogger logger, string page);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Visite terminée : {Pages} page(s), {Questions} question(s), active pendant {ActiveSeconds} s")]
    public static partial void VisitClosed(this ILogger logger, int pages, int questions, int activeSeconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "Question reçue : {Length} caractères")]
    public static partial void QuestionReceived(this ILogger logger, int length);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Hors périmètre : meilleur passage à {BestScore:F3}, sous le seuil de {MinScore:F2} ; modèle non appelé")]
    private static partial void OutOfScope(this ILogger logger, double? bestScore, double minScore);

    [LoggerMessage(Level = LogLevel.Information, Message = "Injection probable dans la question : ni recherche, ni modèle")]
    private static partial void InjectionBlocked(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Recherche : {Passages} passages, meilleur à {BestScore:F3} (seuil {MinScore:F2}) ; vectorisation {EmbeddingMs} ms, base {SearchMs} ms")]
    private static partial void Searched(this ILogger logger, int passages, double? bestScore, double minScore, int embeddingMs, int searchMs);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Rédaction par {Model} ({PromptVersion}) : {InputTokens} + {OutputTokens} tokens, {CostEur} €, {GenerationMs} ms")]
    private static partial void Generated(this ILogger logger, string? model, string promptVersion, long? inputTokens,
        long? outputTokens, decimal? costEur, int generationMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Garde-fou déclenché : {Guardrail}")]
    private static partial void GuardrailTriggered(this ILogger logger, string guardrail);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Réponse : {Outcome}, {Cited} passage(s) cité(s), {TotalMs} ms au total")]
    private static partial void Answered(this ILogger logger, string outcome, int cited, int totalMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Retour envoyé (question jointe : {Attached})")]
    public static partial void FeedbackSent(this ILogger logger, bool attached);

    /// <summary>Les étapes d'une réponse, écrites une fois la réponse prête (le suivi en direct est prévu en S8/S10).</summary>
    public static void AnswerSteps(this ILogger logger, AnswerResult result, double minScore)
    {
        var entry = AnswerLogEntry.From(result, traceId: null);
        var t = result.Timings;
        if (entry.Outcome == AnswerOutcome.Injection)
            logger.InjectionBlocked();
        else if (entry.Outcome == AnswerOutcome.OutOfScope)
            logger.OutOfScope(entry.BestScore, minScore);
        else
        {
            logger.Searched(entry.Passages, entry.BestScore, minScore, Ms(t.Embedding), Ms(t.Search));
            logger.Generated(entry.Model, result.PromptVersion, entry.InputTokens, entry.OutputTokens, entry.CostEur, Ms(t.Generation));
        }

        foreach (var code in entry.Guardrails.Where(c => c != AnswerService.OutOfScopeCode && c != AnswerService.InjectionCode))
            logger.GuardrailTriggered(code);
        logger.Answered(entry.Outcome, entry.Cited, Ms(t.Total));
    }

    private static int Ms(TimeSpan duration) => (int)Math.Round(duration.TotalMilliseconds);
}

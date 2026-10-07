namespace Limpide.Core.Protection;

/// <param name="PerClientPerHour">Questions par visiteur (adresse IP) sur une fenêtre d'une heure.</param>
/// <param name="GlobalPerDay">
/// Questions par jour (UTC), tous visiteurs confondus : borne le coût même si l'abus vient de nombreuses adresses.
/// À 0,5 centime par question (Mistral Medium), 60 par jour font au plus 9 € par mois.
/// </param>
public sealed record QuotaOptions(int PerClientPerHour = 10, int GlobalPerDay = 60);

public enum QuotaVerdict
{
    Allowed,
    ClientLimitReached,
    GlobalLimitReached,
}

/// <param name="RetryAfter">Délai avant qu'une nouvelle question soit acceptée (zéro si autorisée).</param>
/// <param name="Remaining">Questions restantes pour ce visiteur dans la fenêtre en cours.</param>
public sealed record QuotaDecision(QuotaVerdict Verdict, TimeSpan RetryAfter, int Remaining)
{
    public bool Allowed => Verdict == QuotaVerdict.Allowed;
}

/// <summary>
/// Quota de questions de la démo publique, en mémoire (une seule instance de l'application ; remis à zéro au
/// redémarrage). Fenêtres fixes : par visiteur, une heure à partir de sa première question ; global, le jour UTC.
/// Une question refusée ne consomme rien.
/// </summary>
/// <remarks>
/// Avec Blazor Server, les questions passent par la connexion WebSocket et non par des requêtes HTTP :
/// le limiteur HTTP d'ASP.NET Core ne les voit pas, d'où ce quota appliqué au moment de poser la question.
/// </remarks>
public sealed class QuestionQuota(QuotaOptions options, TimeProvider clock)
{
    private static readonly TimeSpan ClientWindow = TimeSpan.FromHours(1);

    private readonly Lock gate = new();
    private readonly Dictionary<string, (DateTimeOffset Start, int Count)> clients = [];
    private (DateOnly Day, int Count) global;

    public QuotaDecision TryConsume(string client)
    {
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        lock (gate)
        {
            if (clients.Count > 10_000)
                PurgeExpired(now);

            if (global.Day != today)
                global = (today, 0);

            var window = clients.TryGetValue(client, out var current) && now - current.Start < ClientWindow
                ? current
                : (Start: now, Count: 0);

            if (window.Count >= options.PerClientPerHour)
                return new QuotaDecision(QuotaVerdict.ClientLimitReached, window.Start + ClientWindow - now, 0);

            if (global.Count >= options.GlobalPerDay)
            {
                var midnight = new DateTimeOffset(today.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
                return new QuotaDecision(QuotaVerdict.GlobalLimitReached, midnight - now, options.PerClientPerHour - window.Count);
            }

            clients[client] = (window.Start, window.Count + 1);
            global = (today, global.Count + 1);
            return new QuotaDecision(QuotaVerdict.Allowed, TimeSpan.Zero, options.PerClientPerHour - window.Count - 1);
        }
    }

    private void PurgeExpired(DateTimeOffset now)
    {
        foreach (var expired in clients.Where(c => now - c.Value.Start >= ClientWindow).Select(c => c.Key).ToList())
            clients.Remove(expired);
    }
}

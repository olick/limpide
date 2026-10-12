using System.Security.Claims;

namespace Limpide.Core.Administration;

/// <summary>Auteur d'une décision (journal admin_actions) : une personne connectée au back office, ou la console.</summary>
/// <param name="Id">Identifiant stable : « émetteur#sujet » pour une personne, « console » pour la console d'ingestion.</param>
/// <param name="Name">Nom affiché, pour la lecture du journal.</param>
public sealed record Actor(string Id, string Name)
{
    public static readonly Actor Console = new("console", "console d'ingestion");

    /// <summary>
    /// Personne connectée par OpenID Connect. Le standard garantit que le couple (émetteur, sujet) identifie une
    /// personne de façon stable chez tout fournisseur conforme : c'est l'identifiant retenu, jamais une information
    /// propre à un fournisseur (adresse e-mail modifiable, identifiant maison). Null si l'utilisateur n'a pas de sujet.
    /// </summary>
    public static Actor? From(ClaimsPrincipal user)
    {
        var subject = user.FindFirst("sub");
        if (subject is null || string.IsNullOrWhiteSpace(subject.Value))
            return null;

        var name = user.FindFirst("name")?.Value ?? user.FindFirst("preferred_username")?.Value ?? subject.Value;
        return new Actor($"{subject.Issuer}#{subject.Value}", name);
    }

    /// <summary>Accès au back office : identifiant présent, exactement, dans la liste de la configuration.</summary>
    public bool IsIn(IEnumerable<string> allowed) => allowed.Contains(Id, StringComparer.Ordinal);
}

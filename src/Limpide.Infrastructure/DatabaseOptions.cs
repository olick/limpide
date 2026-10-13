namespace Limpide.Infrastructure;

/// <summary>
/// Section « Database » de la configuration : comment s'authentifier auprès de PostgreSQL. Par défaut, le mot de passe
/// de la chaîne de connexion (poste, VPS). Sur Azure (S6), aucun mot de passe : un jeton Entra ID, demandé par
/// l'identité managée de l'application et renouvelé avant son expiration.
/// </summary>
public sealed class DatabaseOptions
{
    public const string Password = "Password";
    public const string EntraId = "EntraId";

    /// <summary><c>Password</c> (défaut) ou <c>EntraId</c>.</summary>
    public string Authentication { get; set; } = Password;

    /// <summary>
    /// Identifiant client de l'identité managée attribuée à l'application (Azure). Vide : l'identité disponible
    /// sur la machine (session <c>az login</c> sur le poste, par exemple).
    /// </summary>
    public string? ManagedIdentityClientId { get; set; }

    public bool UsesEntraId => Authentication switch
    {
        Password => false,
        EntraId => true,
        _ => throw new InvalidOperationException($"Database:Authentication « {Authentication} » inconnu (attendu : {Password} ou {EntraId})."),
    };
}

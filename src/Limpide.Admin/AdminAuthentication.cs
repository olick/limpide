using Limpide.Core.Administration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Limpide.Admin;

/// <summary>
/// Section « Admin » de la configuration. Le fournisseur d'identité n'apparaît qu'ici, par trois valeurs : changer de
/// fournisseur (Microsoft, Google, Keycloak, Pocket ID, Authelia...) = changer la configuration, pas le code.
/// </summary>
public sealed class AdminSettings
{
    /// <summary>Adresse du fournisseur OpenID Connect (ex. https://login.microsoftonline.com/consumers/v2.0).</summary>
    public string? Authority { get; init; }

    public string? ClientId { get; init; }

    public string? ClientSecret { get; init; }

    /// <summary>Personnes autorisées, par identifiant « émetteur#sujet » (voir <see cref="Actor.From"/>).</summary>
    public string[] AllowedUsers { get; init; } = [];

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Authority) && !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

/// <summary>
/// Connexion au back office : OpenID Connect générique (code d'autorisation + PKCE), session dans un cookie, accès
/// réservé aux identifiants de la liste. Le reste de l'application ne voit qu'un utilisateur et la politique « admin ».
/// </summary>
public static class AdminAuthentication
{
    public const string Policy = "admin";

    public static IServiceCollection AddAdminAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("Admin").Get<AdminSettings>() ?? new AdminSettings();
        services.AddSingleton(settings);

        var authentication = services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                // Sans fournisseur configuré, une page protégée renvoie à l'accueil, qui l'explique.
                options.DefaultChallengeScheme = settings.IsConfigured
                    ? OpenIdConnectDefaults.AuthenticationScheme
                    : CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = "limpide-admin";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax; // nécessaire au retour du fournisseur (redirection)
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // HTTPS en production, derrière Caddy
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.AccessDeniedPath = "/refuse";
                options.LoginPath = "/";
            });

        // Sans configuration (poste de développement sans secrets), l'application démarre et la page d'accueil le dit.
        if (settings.IsConfigured)
        {
            authentication.AddOpenIdConnect(options =>
            {
                options.Authority = settings.Authority;
                options.ClientId = settings.ClientId;
                options.ClientSecret = settings.ClientSecret;
                options.ResponseType = OpenIdConnectResponseType.Code; // flux « code d'autorisation », PKCE par défaut
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.MapInboundClaims = false; // noms de claims du standard (sub, name), sans traduction Microsoft
                options.TokenValidationParameters.NameClaimType = "name";
                options.SaveTokens = false; // aucun appel d'API au nom de l'utilisateur : inutile de garder ses jetons
            });
        }

        services.AddAuthorization(options => options.AddPolicy(Policy, policy => policy
            .RequireAuthenticatedUser()
            .RequireAssertion(context => Actor.From(context.User)?.IsIn(settings.AllowedUsers) == true)));
        services.AddCascadingAuthenticationState();
        return services;
    }
}

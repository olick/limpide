using System.Text;
using Limpide.Admin;
using Limpide.Admin.Components;
using Limpide.Infrastructure;
using Limpide.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// Mêmes réglages et mêmes stores que la console et le web.
builder.Configuration.AddLimpideSharedSettings();
builder.Services.AddLimpideInfrastructure(builder.Configuration);
builder.Services.AddAdminAuthentication(builder.Configuration);

// Clés de chiffrement des cookies conservées hors du conteneur (volume) : sans elles, chaque déploiement
// déconnecterait l'administrateur et écrirait des erreurs « antiforgery token could not be decrypted ».
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keys)
    builder.Services.AddDataProtection().SetApplicationName("limpide-admin").PersistKeysToFileSystem(new DirectoryInfo(keys));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Connexion : redirige vers le fournisseur d'identité, puis revient sur une page locale seulement.
app.MapGet("/connexion", (string? retour, AdminSettings settings) => settings.IsConfigured
    ? Results.Challenge(new AuthenticationProperties { RedirectUri = retour is { Length: > 0 } && retour.StartsWith('/') && !retour.StartsWith("//") ? retour : "/" })
    : Results.LocalRedirect("/"));

app.MapGet("/deconnexion", async (HttpContext context) =>
{
    // Ferme la session du back office (pas celle du fournisseur d'identité, qui sert à d'autres sites).
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/");
});

app.MapGet("/retours/export.csv", async (FeedbackStore store, CancellationToken ct) =>
{
    var csv = new StringBuilder("date;compris;trompeur;sous_le_capot;question;reponse;modele;consignes;cites;traite_le;traite_par\n");
    foreach (var item in await store.ListAsync(10_000, ct))
    {
        var f = item.Feedback;
        string[] fields =
        [
            item.CreatedAt.ToString("yyyy-MM-dd HH:mm"), f.Understood ?? "", f.Misleading ?? "", f.UnderTheHood ?? "",
            f.Context?.Question ?? "", f.Context?.Answer ?? "", f.Context?.Model ?? "", f.Context?.PromptVersion ?? "",
            string.Join(" | ", f.Context?.Cited ?? []), item.HandledAt?.ToString("yyyy-MM-dd HH:mm") ?? "", item.HandledBy ?? "",
        ];
        csv.AppendJoin(';', fields.Select(Csv)).Append('\n');
    }

    return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
        "text/csv; charset=utf-8", $"limpide-retours-{DateTime.UtcNow:yyyyMMdd}.csv");
}).RequireAuthorization(AdminAuthentication.Policy);

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Champ CSV : guillemets doublés ; un texte de visiteur qui commence par = + - @ serait interprété comme une
// formule par le tableur (injection) : on le préfixe d'une apostrophe.
static string Csv(string value)
{
    if (value.Length > 0 && "=+-@\t\r".Contains(value[0]))
        value = "'" + value;
    return $"\"{value.Replace("\"", "\"\"")}\"";
}

using Limpide.Core.Protection;
using Limpide.Infrastructure;
using Limpide.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Mêmes réglages que la console (recherche, seuil, modèle, tarifs) et mêmes services.
builder.Configuration.AddLimpideSharedSettings();
builder.Services.AddLimpideInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Limpide.Web.EmbeddingWarmup>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(builder.Configuration.GetSection("Protection").Get<QuotaOptions>() ?? new QuotaOptions());
builder.Services.AddSingleton<QuestionQuota>();
// Anti-spam du formulaire de retours : 5 envois par heure et par visiteur, 200 par jour au total.
builder.Services.AddKeyedSingleton("feedback", (services, _) =>
    new QuestionQuota(new QuotaOptions(PerClientPerHour: 5, GlobalPerDay: 200), services.GetRequiredService<TimeProvider>()));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

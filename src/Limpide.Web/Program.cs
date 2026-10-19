using Limpide.Core.Answering;
using Limpide.Core.Protection;
using Limpide.Infrastructure;
using Limpide.Infrastructure.Storage;
using Limpide.Web.Components;
using Microsoft.AspNetCore.DataProtection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Mêmes réglages que la console (recherche, seuil, modèle, tarifs) et mêmes services.
builder.Configuration.AddLimpideSharedSettings();
builder.Services.AddLimpideInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Limpide.Web.EmbeddingWarmup>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(builder.Configuration.GetSection("Protection").Get<QuotaOptions>() ?? new QuotaOptions());
builder.Services.AddSingleton<QuestionQuota>();
// Une visite par circuit Blazor : identifiant aléatoire qui relie ses messages (ADR-018), ni IP ni cookie.
builder.Services.AddScoped<Limpide.Web.Visit>();
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler, Limpide.Web.VisitCircuitHandler>();
// Page publique d'évaluation : rapports versionnés copiés avec l'application (dossier evaluation).
builder.Services.AddSingleton<Limpide.Web.EvaluationReports>();
// Anti-spam du formulaire de retours : 5 envois par heure et par visiteur, 200 par jour au total.
builder.Services.AddKeyedSingleton("feedback", (services, _) =>
    new QuestionQuota(new QuotaOptions(PerClientPerHour: 5, GlobalPerDay: 200), services.GetRequiredService<TimeProvider>()));

// Clés de chiffrement des cookies conservées hors du conteneur (volume) : sans elles, chaque déploiement écrivait
// « antiforgery token could not be decrypted » dans les journaux (S5).
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keys)
    builder.Services.AddDataProtection().SetApplicationName("limpide-web").PersistKeysToFileSystem(new DirectoryInfo(keys));

// Supervision (ADR-018) : traces, métriques et journaux envoyés en OTLP au collecteur, seulement si son adresse est
// configurée (variable OTEL_EXPORTER_OTLP_ENDPOINT). Sans elle, rien n'est mesuré ni exporté, et le journal des
// réponses n'a pas de trace_id : la démo fonctionne de la même façon.
if (builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] is { Length: > 0 })
{
    string[] blazor = ["Microsoft.AspNetCore.Components", "Microsoft.AspNetCore.Components.Server.Circuits"];
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService("limpide-web"))
        .WithTracing(tracing => tracing
            .AddSource(AnswerTelemetry.SourceName, ServiceCollectionExtensions.GenAiTelemetrySource, "Npgsql")
            // Une question passe par le circuit SignalR, pas par une requête HTTP : sa trace part de l'activité de
            // l'événement Blazor (.NET 10, « Event onsubmit -> Home.AskAsync »), dont Home.razor relève le trace_id.
            .AddSource(blazor)
            .AddAspNetCoreInstrumentation(options =>
                // Fichiers statiques : du bruit, pas des requêtes de visiteurs.
                options.Filter = context => !Path.HasExtension(context.Request.Path.Value))
            .AddHttpClientInstrumentation())
        .WithMetrics(metrics => metrics
            .AddMeter(ServiceCollectionExtensions.GenAiTelemetrySource, "Npgsql")
            .AddMeter(blazor)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation())
        .WithLogging(configureBuilder: null, configureOptions: options =>
        {
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
        })
        .UseOtlpExporter();
}

builder.Services.AddOutputCache();
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
app.UseOutputCache();

app.MapStaticAssets();
// Test de fumée (S7, 4b-2) : l'application lit-elle la base ? Empreinte du corpus servi (versions, passages, hachage),
// déjà publique dans les rapports d'évaluation. Échec de connexion (Entra ID sur Azure, mot de passe sur le VPS) : 500.
// Réponse gardée 30 s (seules les réponses 200 le sont) : un point public ne doit pas lancer une requête par appel.
app.MapGet("/sante", async (ChunkStore chunks, CancellationToken ct) => await chunks.FingerprintAsync(ct))
    .CacheOutput(policy => policy.Expire(TimeSpan.FromSeconds(30)));
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

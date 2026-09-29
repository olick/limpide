using Limpide.Ingestion;
using Limpide.Ingestion.Extract;
using Limpide.Ingestion.Fetch;
using Limpide.Ingestion.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

// Usage : dotnet run --project src/Limpide.Ingestion -- <commande>
// À lancer depuis la racine du dépôt : les chemins de la configuration sont relatifs au répertoire courant.
var command = args.FirstOrDefault();
if (command is not ("fetch" or "extract"))
{
    Console.Error.WriteLine("""
        Usage : Limpide.Ingestion <commande>

        Commandes, dans l'ordre :
          fetch     télécharge le corpus et enregistre les nouvelles versions
          extract   extrait le texte structuré des versions courantes (data/extracted)
        """);
    return 2;
}

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args[1..],
    // appsettings.json est à côté de l'exécutable, pas dans le répertoire courant.
    ContentRootPath = AppContext.BaseDirectory,
});

var options = builder.Configuration.GetSection("Ingestion").Get<IngestionOptions>() ?? new IngestionOptions();
var connectionString = builder.Configuration.GetConnectionString("Rag")
    ?? throw new InvalidOperationException("Chaîne de connexion « ConnectionStrings:Rag » absente.");

builder.Services.AddSingleton(options);
builder.Services.AddSingleton(_ => new NpgsqlDataSourceBuilder(connectionString).Build());
builder.Services.AddSingleton(_ => FetchCommand.CreateHttpClient(options));
builder.Services.AddSingleton<DocumentVersionStore>();
builder.Services.AddSingleton<FetchCommand>();
builder.Services.AddSingleton<ExtractCommand>();

using var host = builder.Build();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

return command switch
{
    "fetch" => await host.Services.GetRequiredService<FetchCommand>().RunAsync(cts.Token),
    _ => await host.Services.GetRequiredService<ExtractCommand>().RunAsync(cts.Token),
};

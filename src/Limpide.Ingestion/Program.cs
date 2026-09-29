using Limpide.Ingestion;
using Limpide.Ingestion.Chunking;
using Limpide.Ingestion.Embed;
using Limpide.Ingestion.Extract;
using Limpide.Ingestion.Fetch;
using Limpide.Ingestion.Storage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OllamaSharp;
using Pgvector.Npgsql;

// Usage : dotnet run --project src/Limpide.Ingestion -- <commande>
// À lancer depuis la racine du dépôt : les chemins de la configuration sont relatifs au répertoire courant.
var command = args.FirstOrDefault();
if (command is not ("fetch" or "extract" or "chunk" or "embed"))
{
    Console.Error.WriteLine("""
        Usage : Limpide.Ingestion <commande>

        Commandes, dans l'ordre :
          fetch     télécharge le corpus et enregistre les nouvelles versions
          extract   extrait le texte structuré des versions courantes (data/extracted)
          chunk     découpe le texte extrait en passages (table chunks)
          embed     calcule les embeddings des passages qui n'en ont pas
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
var embedding = builder.Configuration.GetSection("Embedding").Get<EmbeddingOptions>() ?? new EmbeddingOptions();
var connectionString = builder.Configuration.GetConnectionString("Rag")
    ?? throw new InvalidOperationException("Chaîne de connexion « ConnectionStrings:Rag » absente.");

builder.Services.AddSingleton(options);
builder.Services.AddSingleton(embedding);
builder.Services.AddSingleton(_ =>
{
    var dataSource = new NpgsqlDataSourceBuilder(connectionString);
    dataSource.UseVector();
    return dataSource.Build();
});
// Changer de fournisseur d'embeddings = remplacer cette ligne par une autre implémentation d'IEmbeddingGenerator.
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ => new OllamaApiClient(embedding.Endpoint, embedding.Model));
builder.Services.AddSingleton(_ => FetchCommand.CreateHttpClient(options));
builder.Services.AddSingleton<DocumentVersionStore>();
builder.Services.AddSingleton<FetchCommand>();
builder.Services.AddSingleton<ExtractCommand>();
builder.Services.AddSingleton<ChunkStore>();
builder.Services.AddSingleton<ChunkCommand>();
builder.Services.AddSingleton<EmbeddingStore>();
builder.Services.AddSingleton<EmbedCommand>();

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
    "extract" => await host.Services.GetRequiredService<ExtractCommand>().RunAsync(cts.Token),
    "chunk" => await host.Services.GetRequiredService<ChunkCommand>().RunAsync(cts.Token),
    _ => await host.Services.GetRequiredService<EmbedCommand>().RunAsync(cts.Token),
};

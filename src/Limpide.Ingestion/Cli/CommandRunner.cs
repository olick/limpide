using Limpide.Infrastructure;
using Limpide.Ingestion.Chunking;
using Limpide.Ingestion.Embed;
using Limpide.Ingestion.Extract;
using Limpide.Ingestion.Fetch;
using Limpide.Ingestion.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Limpide.Ingestion.Cli;

/// <summary>
/// Exécute une commande. La configuration et les services sont reconstruits à chaque commande :
/// les réglages « --Section:Cle=valeur » valent pour cette commande seulement, y compris en mode interactif.
/// </summary>
public static class CommandRunner
{
    public static async Task<int> RunAsync(CommandLine line, CancellationToken ct)
    {
        using var host = Build(line.Settings);
        var services = host.Services;

        return line.Name.ToLowerInvariant() switch
        {
            "fetch" => await services.GetRequiredService<FetchCommand>().RunAsync(ct),
            "extract" => await services.GetRequiredService<ExtractCommand>().RunAsync(ct),
            "chunk" => await services.GetRequiredService<ChunkCommand>().RunAsync(ct),
            "embed" => await services.GetRequiredService<EmbedCommand>().RunAsync(ct),
            "search" => await services.GetRequiredService<SearchCommand>().RunAsync(line.Text, ct),
            "evaluate" => await services.GetRequiredService<EvaluateCommand>().RunAsync(ct),
            _ => throw new ArgumentException($"commande inconnue : {line.Name}"),
        };
    }

    private static IHost Build(string[] settings)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = settings,
            // appsettings.json est à côté de l'exécutable, pas dans le répertoire courant.
            ContentRootPath = AppContext.BaseDirectory,
        });

        var options = builder.Configuration.GetSection("Ingestion").Get<IngestionOptions>() ?? new IngestionOptions();
        builder.Services.AddSingleton(options);
        builder.Services.AddLimpideInfrastructure(builder.Configuration);

        builder.Services.AddSingleton(_ => FetchCommand.CreateHttpClient(options));
        builder.Services.AddSingleton<FetchCommand>();
        builder.Services.AddSingleton<ExtractCommand>();
        builder.Services.AddSingleton<ChunkCommand>();
        builder.Services.AddSingleton<EmbedCommand>();
        builder.Services.AddSingleton<SearchCommand>();
        builder.Services.AddSingleton<EvaluateCommand>();

        return builder.Build();
    }
}

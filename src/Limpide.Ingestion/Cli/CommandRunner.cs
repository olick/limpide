using Limpide.Core.Quality;
using Limpide.Infrastructure;
using Limpide.Infrastructure.Storage;
using Limpide.Ingestion.Chunking;
using Limpide.Ingestion.Database;
using Limpide.Ingestion.Embed;
using Limpide.Ingestion.Extract;
using Limpide.Ingestion.Fetch;
using Limpide.Ingestion.Publish;
using Limpide.Ingestion.Review;
using Limpide.Ingestion.Validate;
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
        var summary = new CommandSummary(line.Name.ToLowerInvariant());
        int code;
        using (var host = Build(line.Settings, summary))
            code = await RunAsync(line, host.Services, ct);

        // Après la fermeture de l'hôte, qui vide les journaux : le résumé est bien la dernière ligne (lue par Airflow).
        if (!summary.IsEmpty)
            Console.WriteLine(summary.ToJson(code));
        return code;
    }

    private static async Task<int> RunAsync(CommandLine line, IServiceProvider services, CancellationToken ct) =>
        line.Name.ToLowerInvariant() switch
        {
            "migrate" => services.GetRequiredService<MigrateCommand>().Run(),
            "fetch" => await services.GetRequiredService<FetchCommand>().RunAsync(ct),
            "extract" => await services.GetRequiredService<ExtractCommand>().RunAsync(ct),
            "chunk" => await services.GetRequiredService<ChunkCommand>().RunAsync(ct),
            "validate" => await services.GetRequiredService<ValidateCommand>().RunAsync(ct),
            "embed" => await services.GetRequiredService<EmbedCommand>().RunAsync(ct),
            "publish" => await services.GetRequiredService<PublishCommand>().RunAsync(ct),
            "versions" => await services.GetRequiredService<VersionsCommand>().RunAsync(ct),
            "release" => await services.GetRequiredService<QuarantineDecisionCommand>().RunAsync(line.Text, QuarantineDecision.Release, ct),
            "retry" => await services.GetRequiredService<QuarantineDecisionCommand>().RunAsync(line.Text, QuarantineDecision.Retry, ct),
            "reject" => await services.GetRequiredService<QuarantineDecisionCommand>().RunAsync(line.Text, QuarantineDecision.Reject, ct),
            "ask" => await services.GetRequiredService<AskCommand>().RunAsync(line.Text, ct),
            "search" => await services.GetRequiredService<SearchCommand>().RunAsync(line.Text, ct),
            "trace" => await services.GetRequiredService<TraceCommand>().RunAsync(line.Text, ct),
            "evaluate" => await services.GetRequiredService<EvaluateCommand>().RunAsync(ct),
            "feedback" => await services.GetRequiredService<FeedbackCommand>().RunAsync(ct),
            "evaluate-answers" => await services.GetRequiredService<EvaluateAnswersCommand>().RunAsync(ct),
            "judge-calibrate" => await services.GetRequiredService<JudgeCalibrateCommand>().RunAsync(ct),
            _ => throw new ArgumentException($"commande inconnue : {line.Name}"),
        };

    private static IHost Build(string[] settings, CommandSummary summary)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = settings,
            // appsettings.json est à côté de l'exécutable, pas dans le répertoire courant.
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Configuration.AddLimpideSharedSettings();
        var options = builder.Configuration.GetSection("Ingestion").Get<IngestionOptions>() ?? new IngestionOptions();
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(summary);
        builder.Services.AddLimpideInfrastructure(builder.Configuration);

        builder.Services.AddSingleton(_ => FetchCommand.CreateHttpClient(options));
        builder.Services.AddSingleton<MigrateCommand>();
        builder.Services.AddSingleton<FetchCommand>();
        builder.Services.AddSingleton<ExtractCommand>();
        builder.Services.AddSingleton<ChunkCommand>();
        builder.Services.AddSingleton(builder.Configuration.GetSection("Quality").Get<QualityOptions>() ?? new QualityOptions());
        builder.Services.AddSingleton<ValidateCommand>();
        builder.Services.AddSingleton<EmbedCommand>();
        builder.Services.AddSingleton<PublishCommand>();
        builder.Services.AddSingleton<VersionsCommand>();
        builder.Services.AddSingleton<QuarantineDecisionCommand>();
        builder.Services.AddSingleton<AskCommand>();
        builder.Services.AddSingleton<SearchCommand>();
        builder.Services.AddSingleton<TraceCommand>();
        builder.Services.AddSingleton<EvaluateCommand>();
        builder.Services.AddSingleton<EvaluateAnswersCommand>();
        builder.Services.AddSingleton<JudgeCalibrateCommand>();
        builder.Services.AddSingleton<FeedbackCommand>();

        return builder.Build();
    }
}

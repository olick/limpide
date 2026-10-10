using System.ClientModel;
using Limpide.Core.Answering;
using Limpide.Core.Search;
using Limpide.Infrastructure.Migrations;
using Limpide.Infrastructure.Search;
using Limpide.Infrastructure.Storage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using OllamaSharp;
using OpenAI;
using OpenAI.Chat;
using Pgvector.Npgsql;

namespace Limpide.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Base PostgreSQL (pgvector), embeddings, recherche de passages et génération des réponses, partagés par la
    /// console et l'application web. Lit « ConnectionStrings:Rag », les sections « Embedding » et « Generation »,
    /// et la clé « Mistral:ApiKey ».
    /// </summary>
    public static IServiceCollection AddLimpideInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var embedding = configuration.GetSection("Embedding").Get<EmbeddingOptions>() ?? new EmbeddingOptions();
        var connectionString = configuration.GetConnectionString("Rag")
            ?? throw new InvalidOperationException("Chaîne de connexion « ConnectionStrings:Rag » absente.");

        services.AddSingleton(embedding);
        services.AddSingleton(_ =>
        {
            var dataSource = new NpgsqlDataSourceBuilder(connectionString);
            // Pas de Kerberos : sans cela, Npgsql tente le chiffrement GSS et les images .NET, sans libgssapi,
            // écrivent « libgssapi_krb5.so.2: cannot open shared object file » à chaque démarrage.
            dataSource.ConnectionStringBuilder.GssEncryptionMode = GssEncryptionMode.Disable;
            dataSource.UseVector();
            return dataSource.Build();
        });

        // Changer de fournisseur d'embeddings = remplacer cette ligne par une autre implémentation d'IEmbeddingGenerator.
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ => new OllamaApiClient(embedding.Endpoint, embedding.Model));

        services.AddSingleton(provider => new DatabaseMigrator(connectionString, provider.GetRequiredService<ILogger<DatabaseMigrator>>()));
        services.AddSingleton<DocumentVersionStore>();
        services.AddSingleton<ChunkStore>();
        services.AddSingleton<EmbeddingStore>();
        services.AddSingleton<FeedbackStore>();
        services.AddSingleton(configuration.GetSection("Search").Get<SearchOptions>() ?? new SearchOptions());
        services.AddSingleton<IPassageSearch, PgvectorPassageSearch>();

        var generation = configuration.GetSection("Generation").Get<GenerationOptions>() ?? new GenerationOptions();
        services.AddSingleton(generation.ToSettings());
        // Clé exigée seulement quand on génère une réponse : les commandes d'ingestion s'en passent.
        services.AddSingleton<IChatClient>(_ =>
        {
            var apiKey = configuration["Mistral:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException(
                    "Clé « Mistral:ApiKey » absente (dotnet user-secrets set \"Mistral:ApiKey\" ... --project src/Limpide.Ingestion).");
            return new ChatClient(generation.Model, new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = generation.Endpoint })
                .AsIChatClient();
        });
        services.AddSingleton<AnswerService>();
        return services;
    }
}

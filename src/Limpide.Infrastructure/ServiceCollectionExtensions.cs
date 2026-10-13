using System.ClientModel;
using Azure.Core;
using Azure.Identity;
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
using OpenAI.Embeddings;
using Pgvector.Npgsql;

namespace Limpide.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Base PostgreSQL (pgvector), embeddings, recherche de passages et génération des réponses, partagés par la
    /// console et l'application web. Lit « ConnectionStrings:Rag », les sections « Database », « Embedding » et
    /// « Generation », et la clé « Mistral:ApiKey ».
    /// </summary>
    public static IServiceCollection AddLimpideInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var embedding = configuration.GetSection("Embedding").Get<EmbeddingOptions>() ?? new EmbeddingOptions();
        var database = configuration.GetSection("Database").Get<DatabaseOptions>() ?? new DatabaseOptions();
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
            if (database.UsesEntraId)
                UseEntraIdToken(dataSource, database);
            return dataSource.Build();
        });

        var generation = configuration.GetSection("Generation").Get<GenerationOptions>() ?? new GenerationOptions();

        // Fournisseur d'embeddings choisi par configuration (Embedding:Provider), derrière la même interface.
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ => embedding.Provider switch
        {
            EmbeddingOptions.Ollama => new OllamaApiClient(embedding.Endpoint, embedding.Model),
            // API compatible OpenAI : même modèle bge-m3 chez OVHcloud (vecteurs identiques, mesuré), ou un autre modèle.
            EmbeddingOptions.OpenAI => new EmbeddingClient(embedding.Model,
                new ApiKeyCredential(embedding.ApiKey is { Length: > 0 } key
                    ? key
                    : throw new InvalidOperationException("Clé « Embedding:ApiKey » absente (fournisseur OpenAI).")),
                new OpenAIClientOptions { Endpoint = embedding.Endpoint }).AsIEmbeddingGenerator(),
            _ => throw new InvalidOperationException(
                $"Embedding:Provider « {embedding.Provider} » inconnu (attendu : {EmbeddingOptions.Ollama} ou {EmbeddingOptions.OpenAI})."),
        });

        services.AddSingleton(provider => new DatabaseMigrator(connectionString, provider.GetRequiredService<ILogger<DatabaseMigrator>>()));
        services.AddSingleton<DocumentVersionStore>();
        services.AddSingleton<ChunkStore>();
        services.AddSingleton<EmbeddingStore>();
        services.AddSingleton<FeedbackStore>();
        services.AddSingleton<AuditStore>();
        services.AddSingleton(configuration.GetSection("Search").Get<SearchOptions>() ?? new SearchOptions());
        services.AddSingleton<IPassageSearch, PgvectorPassageSearch>();

        services.AddSingleton(generation.ToSettings());
        // Clé exigée seulement quand on génère une réponse : les commandes d'ingestion s'en passent.
        services.AddSingleton<IChatClient>(_ =>
            new ChatClient(generation.Model, new ApiKeyCredential(MistralApiKey(configuration)), new OpenAIClientOptions { Endpoint = generation.Endpoint })
                .AsIChatClient());
        services.AddSingleton<AnswerService>();
        return services;
    }

    private static string MistralApiKey(IConfiguration configuration) =>
        configuration["Mistral:ApiKey"] is { Length: > 0 } apiKey
            ? apiKey
            : throw new InvalidOperationException(
                "Clé « Mistral:ApiKey » absente (dotnet user-secrets set \"Mistral:ApiKey\" ... --project src/Limpide.Ingestion).");

    /// <summary>
    /// Azure (S6) : le « mot de passe » de chaque nouvelle connexion est un jeton Entra ID de l'identité de
    /// l'application, renouvelé toutes les 30 minutes (il en vaut au moins 60) ; aucun secret dans la configuration.
    /// </summary>
    private static void UseEntraIdToken(NpgsqlDataSourceBuilder dataSource, DatabaseOptions database)
    {
        TokenCredential credential = database.ManagedIdentityClientId is { Length: > 0 } clientId
            ? new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(clientId))
            : new DefaultAzureCredential();
        var postgres = new TokenRequestContext(["https://ossrdbms-aad.database.windows.net/.default"]);

        dataSource.UsePeriodicPasswordProvider(
            async (_, ct) => (await credential.GetTokenAsync(postgres, ct)).Token,
            successRefreshInterval: TimeSpan.FromMinutes(30),
            failureRefreshInterval: TimeSpan.FromSeconds(10));
    }
}

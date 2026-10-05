using Limpide.Core.Search;
using Limpide.Infrastructure.Search;
using Limpide.Infrastructure.Storage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OllamaSharp;
using Pgvector.Npgsql;

namespace Limpide.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Base PostgreSQL (pgvector), générateur d'embeddings et recherche de passages, partagés par la console
    /// d'ingestion et l'application web. Lit « ConnectionStrings:Rag » et la section « Embedding ».
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
            dataSource.UseVector();
            return dataSource.Build();
        });

        // Changer de fournisseur d'embeddings = remplacer cette ligne par une autre implémentation d'IEmbeddingGenerator.
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ => new OllamaApiClient(embedding.Endpoint, embedding.Model));

        services.AddSingleton<DocumentVersionStore>();
        services.AddSingleton<ChunkStore>();
        services.AddSingleton<EmbeddingStore>();
        services.AddSingleton<IPassageSearch, PgvectorPassageSearch>();
        return services;
    }
}

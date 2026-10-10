using DbUp;
using DbUp.Engine;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Limpide.Infrastructure.Migrations;

/// <summary>Résultat d'une migration : scripts appliqués cette fois, et scripts déjà présents dans le journal.</summary>
public sealed record MigrationResult(IReadOnlyList<string> Applied, int AlreadyApplied);

/// <summary>
/// Fait évoluer le schéma avec les scripts numérotés de Migrations/ (DbUp). Chaque script s'exécute une seule fois
/// par base, dans sa propre transaction ; la table schemaversions garde la liste des scripts appliqués.
/// </summary>
public sealed class DatabaseMigrator(string connectionString, ILogger<DatabaseMigrator> logger)
{
    private const string ScriptPrefix = "Limpide.Infrastructure.Migrations.";

    public IReadOnlyList<string> Pending() => Build().GetScriptsToExecute().Select(Name).ToList();

    public MigrationResult Migrate()
    {
        var engine = Build();
        var already = engine.GetExecutedScripts().Count;
        var result = engine.PerformUpgrade();
        if (!result.Successful)
            throw new InvalidOperationException($"migration {Name(result.ErrorScript)} en échec : {result.Error.Message}", result.Error);

        return new MigrationResult(result.Scripts.Select(Name).ToList(), already);
    }

    private UpgradeEngine Build()
    {
        // Comme NpgsqlDataSource (ServiceCollectionExtensions) : pas de chiffrement GSS, absent des images .NET.
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { GssEncryptionMode = GssEncryptionMode.Disable };

        return DeployChanges.To
            .PostgresqlDatabase(builder.ConnectionString)
            .WithScriptsEmbeddedInAssembly(typeof(DatabaseMigrator).Assembly, name => name.StartsWith(ScriptPrefix) && name.EndsWith(".sql"))
            .WithTransactionPerScript()
            .LogTo(logger)
            .Build();
    }

    private static string Name(SqlScript? script) => script?.Name[ScriptPrefix.Length..] ?? "(inconnue)";
}

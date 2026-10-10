using Limpide.Infrastructure.Migrations;
using Limpide.Ingestion.Cli;
using Microsoft.Extensions.Logging;

namespace Limpide.Ingestion.Database;

/// <summary>
/// Applique au schéma les migrations qui manquent (scripts numérotés de Limpide.Infrastructure/Migrations).
/// Lancée explicitement à chaque déploiement, jamais au démarrage de l'application web : un changement de schéma
/// de production reste un acte visible. Relançable sans effet quand tout est déjà appliqué.
/// </summary>
public sealed class MigrateCommand(DatabaseMigrator migrator, CommandSummary summary, ILogger<MigrateCommand> logger)
{
    public int Run()
    {
        var result = migrator.Migrate();
        summary.Add("applied", result.Applied.Count);
        summary.Add("alreadyApplied", result.AlreadyApplied);

        if (result.Applied.Count == 0)
            logger.LogInformation("Schéma à jour : {Count} migration(s) déjà appliquée(s)", result.AlreadyApplied);
        else
            logger.LogInformation("Migration(s) appliquée(s) : {Scripts}", string.Join(", ", result.Applied));
        return 0;
    }
}

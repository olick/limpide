using Limpide.Core.Documentation;

namespace Limpide.Web;

/// <summary>
/// Index des décisions d'architecture copié à côté de l'application (dossier <c>adr</c>, voir Limpide.Web.csproj), lu
/// une fois au démarrage : la page « Comment il est construit » montre la liste du dépôt, jamais une copie à tenir.
/// </summary>
public sealed class ArchitectureDecisions
{
    public IReadOnlyList<ArchitectureDecision> All { get; }

    public ArchitectureDecisions(ILogger<ArchitectureDecisions> logger)
    {
        var file = Path.Combine(AppContext.BaseDirectory, "adr", "README.md");
        All = File.Exists(file) ? ArchitectureDecisionIndex.Parse(File.ReadAllText(file)) : [];
        if (All.Count == 0)
            logger.LogWarning("Index des décisions d'architecture absent ou vide : {File}", file);
    }
}

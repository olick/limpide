using System.Text;

namespace Limpide.Ingestion.Cli;

public sealed record CommandInfo(string Name, string Usage, string Description, string Group, bool RequiresText = false);

/// <summary>Commandes connues : source unique de l'aide et de la validation.</summary>
public static class CommandCatalog
{
    public const string Ingestion = "Ingestion, dans l'ordre";
    public const string Search = "Questions et recherche";
    public const string Session = "Mode interactif";

    public static readonly IReadOnlyList<CommandInfo> All =
    [
        new("fetch", "fetch", "télécharge le corpus (corpus.json) et enregistre les nouvelles versions", Ingestion),
        new("extract", "extract", "extrait le texte structuré des versions courantes (data/extracted)", Ingestion),
        new("chunk", "chunk", "découpe le texte extrait en passages (table chunks)", Ingestion),
        new("embed", "embed", "calcule les embeddings des passages qui n'en ont pas", Ingestion),
        new("ask", "ask <question>", "répond à la question en citant les textes (recherche + Mistral)", Search, RequiresText: true),
        new("search", "search <question>", "affiche les 5 passages les plus proches de la question", Search, RequiresText: true),
        new("evaluate", "evaluate", "score de la recherche sur le jeu de questions (eval/questions.json)", Search),
        new("help", "help", "affiche cette aide", Session),
        new("exit", "exit", "quitte (aussi : quit, Ctrl+D)", Session),
    ];

    public static CommandInfo? Find(string name) =>
        All.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public static string Help()
    {
        var text = new StringBuilder("""
            Limpide — console d'ingestion et de recherche

            Sans argument : mode interactif (invite « limpide> »). Avec une commande : l'exécute puis s'arrête.
              dotnet run --project src/Limpide.Ingestion -- <commande> [texte] [--Section:Cle=valeur ...]

            """);

        foreach (var group in All.GroupBy(c => c.Group))
        {
            text.AppendLine().AppendLine($"{group.Key} :");
            foreach (var command in group)
                text.AppendLine($"  {command.Usage,-20} {command.Description}");
        }

        text.AppendLine().Append("""
            Réglages ponctuels : ajouter --Section:Cle=valeur à la commande, ex.
              embed --Embedding:IncludeHeading=true
              evaluate --Embedding:IncludeHeading=true

            Guillemets facultatifs pour la question de ask et search ; seuls les guillemets doubles regroupent
            (l'apostrophe de « d'IA » est du texte). Ctrl+C interrompt la commande en cours.
            À lancer depuis la racine du dépôt : les chemins de la configuration en dépendent.
            """);
        return text.ToString();
    }
}

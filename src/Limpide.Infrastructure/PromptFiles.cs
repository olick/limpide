using System.Text.RegularExpressions;

namespace Limpide.Infrastructure;

/// <summary>
/// Consignes des modèles (S7) : src/prompts/&lt;famille&gt;/&lt;n&gt;.txt, copiés à côté de chaque exécutable comme
/// appsettings.shared.json. La version choisie par configuration (« answer/3 ») est le chemin du fichier : un fichier
/// publié ne se modifie plus, on en ajoute un nouveau.
/// </summary>
public static partial class PromptFiles
{
    public const string Directory = "prompts";

    public static string Read(string version)
    {
        if (!VersionFormat().IsMatch(version))
            throw new InvalidOperationException($"Version de consignes invalide : « {version} » (attendu : famille/numéro, ex. answer/3).");
        var path = Path.Combine(AppContext.BaseDirectory, Directory, $"{version}.txt");
        return File.Exists(path)
            ? File.ReadAllText(path)
            : throw new InvalidOperationException($"Consignes {version} introuvables : {path}.");
    }

    [GeneratedRegex(@"^[a-z]+/[0-9]+$")]
    private static partial Regex VersionFormat();
}

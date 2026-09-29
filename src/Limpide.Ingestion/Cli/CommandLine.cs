using System.Text;

namespace Limpide.Ingestion.Cli;

/// <summary>
/// Une commande saisie : son nom, le texte libre (la question de search) et les réglages « --Section:Cle=valeur ».
/// </summary>
public sealed record CommandLine(string Name, string Text, string[] Settings)
{
    /// <summary>Depuis les arguments du processus, déjà découpés par le shell.</summary>
    public static CommandLine? FromArgs(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
            return null;

        var rest = args.Skip(1).ToList();
        return new CommandLine(
            args[0],
            string.Join(' ', rest.Where(a => !a.StartsWith("--"))),
            rest.Where(a => a.StartsWith("--")).ToArray());
    }

    /// <summary>Depuis une ligne saisie au clavier.</summary>
    public static CommandLine? Parse(string line) => FromArgs(Tokenize(line));

    /// <summary>
    /// Découpe sur les espaces ; seuls les guillemets doubles regroupent plusieurs mots.
    /// L'apostrophe n'est pas un délimiteur : « Quelles pratiques d'IA » reste du texte.
    /// </summary>
    public static IReadOnlyList<string> Tokenize(string line)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        var inToken = false;

        foreach (var c in line)
        {
            if (c == '"')
            {
                quoted = !quoted;
                inToken = true;
            }
            else if (char.IsWhiteSpace(c) && !quoted)
            {
                if (inToken)
                    tokens.Add(current.ToString());
                current.Clear();
                inToken = false;
            }
            else
            {
                current.Append(c);
                inToken = true;
            }
        }

        if (inToken)
            tokens.Add(current.ToString());
        return tokens;
    }
}

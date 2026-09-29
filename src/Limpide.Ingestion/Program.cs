using Limpide.Ingestion.Cli;

// Avec une commande : l'exécute puis s'arrête, avec un code de sortie (mode utilisé par Airflow en semaine 4).
// Sans argument : mode interactif, jusqu'à « exit ». Voir « help ».
// À lancer depuis la racine du dépôt : les chemins de la configuration sont relatifs au répertoire courant.

CancellationTokenSource? running = null;
Console.CancelKeyPress += (_, e) =>
{
    // Ctrl+C interrompt la commande en cours ; à l'invite, il quitte.
    if (running is not null)
    {
        e.Cancel = true;
        running.Cancel();
    }
};

if (CommandLine.FromArgs(args) is { } oneShot)
    return await ExecuteAsync(oneShot);

Console.WriteLine("Limpide — mode interactif. « help » pour la liste des commandes, « exit » pour quitter.");
while (true)
{
    Console.Write("\nlimpide> ");
    var input = Console.ReadLine();
    if (input is null) // Ctrl+D
        return 0;

    if (CommandLine.Parse(input) is not { } line)
        continue;
    if (line.Name is "exit" or "quit")
        return 0;

    var code = await ExecuteAsync(line);
    if (code != 0)
        Console.WriteLine($"(code de sortie {code})");
}

async Task<int> ExecuteAsync(CommandLine line)
{
    var command = CommandCatalog.Find(line.Name);
    if (command is null)
    {
        Console.Error.WriteLine($"Commande inconnue : {line.Name} (« help » pour la liste des commandes)");
        return 2;
    }

    if (command.Name == "help")
    {
        Console.WriteLine(CommandCatalog.Help());
        return 0;
    }

    if (command.RequiresText && string.IsNullOrWhiteSpace(line.Text))
    {
        Console.Error.WriteLine($"Usage : {command.Usage}");
        return 2;
    }

    running = new CancellationTokenSource();
    try
    {
        return await CommandRunner.RunAsync(line, running.Token);
    }
    catch (OperationCanceledException) when (running.IsCancellationRequested)
    {
        Console.Error.WriteLine("Commande interrompue.");
        return 130;
    }
    catch (Exception ex) when (ex is not OutOfMemoryException)
    {
        // En mode interactif, une erreur (base arrêtée, Ollama injoignable...) ne doit pas fermer la session.
        Console.Error.WriteLine($"Erreur ({ex.GetType().Name}) : {ex.Message}");
        return 1;
    }
    finally
    {
        running.Dispose();
        running = null;
    }
}

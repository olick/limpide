using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.FileProviders;

namespace Limpide.Infrastructure;

/// <summary>
/// Réglages communs à la console et à l'application web (src/appsettings.shared.json, copié à côté de chaque
/// exécutable) : connexion, embeddings, recherche, génération. Un seuil ou un tarif ne peut pas diverger
/// entre les deux applications.
/// </summary>
public static class SharedSettings
{
    public const string FileName = "appsettings.shared.json";

    /// <summary>
    /// Ajoute le fichier partagé en tête des sources, donc avec la priorité la plus basse : appsettings.json
    /// du projet, user-secrets, variables d'environnement et ligne de commande peuvent le surcharger.
    /// </summary>
    public static IConfigurationBuilder AddLimpideSharedSettings(this IConfigurationBuilder configuration)
    {
        configuration.Sources.Insert(0, new JsonConfigurationSource
        {
            Path = FileName,
            Optional = false,
            FileProvider = new PhysicalFileProvider(AppContext.BaseDirectory),
        });
        return configuration;
    }
}

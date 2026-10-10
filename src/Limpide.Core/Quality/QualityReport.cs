namespace Limpide.Core.Quality;

/// <summary>Un contrôle : sa valeur mesurée, sa limite, et la règle en clair (affichée dans l'alerte et le back office).</summary>
/// <param name="Name">Identifiant stable (ex. « langue-francaise »).</param>
/// <param name="Detail">Exemple ou précision (ex. le premier passage en cause) ; « sans objet » pour un contrôle non applicable.</param>
public sealed record QualityCheck(string Name, bool Passed, double Value, double Limit, string Rule, string? Detail = null);

/// <summary>Résultat des contrôles qualité d'une version, enregistré dans document_versions.quality_report.</summary>
public sealed record QualityReport(IReadOnlyList<QualityCheck> Checks)
{
    public bool Passed => Checks.All(c => c.Passed);

    public IEnumerable<QualityCheck> Failures => Checks.Where(c => !c.Passed);

    /// <summary>Résumé des échecs, pour status_reason et l'alerte (ex. « langue-francaise : 0,02 (au moins 0,2) »).</summary>
    public string Summary() =>
        Passed ? "tous les contrôles réussis" : string.Join(" ; ", Failures.Select(c => $"{c.Name} : {c.Rule}, mesuré {Format(c.Value)}"));

    /// <summary>Virgule décimale, sans dépendre des cultures installées (l'image .NET de production n'en a pas).</summary>
    public static string Format(double value) =>
        value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
}

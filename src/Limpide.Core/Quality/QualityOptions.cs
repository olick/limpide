namespace Limpide.Core.Quality;

/// <summary>
/// Seuils des contrôles qualité (section « Quality » de la configuration). Calibrés le 2026-10-10 sur les trois
/// documents du corpus, avec une marge mesurée : voir docs/notes/observations-qualite.md.
/// </summary>
public sealed class QualityOptions
{
    /// <summary>Texte extrait minimal, en caractères (corpus : 26 798 à 640 907 ; une page d'erreur : quelques centaines).</summary>
    public int MinTextLength { get; init; } = 2_000;

    /// <summary>Part minimale de mots outils français (le, de, et...) parmi les mots (corpus : ≈ 0,40 ; un texte anglais : 0).</summary>
    public double MinFrenchShare { get; init; } = 0.20;

    /// <summary>Écart maximal du nombre de passages avec la version publiée (corpus : 0).</summary>
    public double MaxPassageCountChange { get; init; } = 0.30;

    /// <summary>Bornes visées par le découpage (Packer) : un passage hors de ces bornes n'est pas une erreur en soi.</summary>
    public int TargetMinLength { get; init; } = 500;

    public int TargetMaxLength { get; init; } = 1_500;

    /// <summary>Part maximale de passages hors des bornes visées (corpus : 4 à 12 %).</summary>
    public double MaxOutOfBoundsShare { get; init; } = 0.20;

    /// <summary>Part maximale de passages au contenu identique à un autre (corpus : 0).</summary>
    public double MaxDuplicateShare { get; init; } = 0.05;

    /// <summary>AI Act : nombre minimal d'articles distincts (corpus : 113).</summary>
    public int MinArticles { get; init; } = 100;

    /// <summary>Fiches CNIL : nombre minimal d'intertitres, niveau 2 ou plus (corpus : 8 et 17).</summary>
    public int MinSections { get; init; } = 4;
}

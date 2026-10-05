namespace Limpide.Infrastructure;

public enum SearchStrategy
{
    /// <summary>Similarité cosinus entre la question et les passages (bge-m3).</summary>
    Vector,

    /// <summary>
    /// Vectorielle + plein texte (titre et texte) pondéré par la rareté des mots, fusionnées par les rangs (RRF).
    /// Mesurée moins bonne que la vectorielle seule (ADR-005) : gardée pour être remesurée sur un jeu plus large.
    /// </summary>
    Hybrid,
}

/// <summary>Section « Search » de la configuration.</summary>
public sealed class SearchOptions
{
    public SearchStrategy Strategy { get; set; } = SearchStrategy.Vector;

    /// <summary>Hybride : candidats retenus par chaque recherche avant la fusion.</summary>
    public int Candidates { get; set; } = 40;

    /// <summary>
    /// Hybride : constante k de la fusion RRF, score = Σ 1 / (k + rang). 60 est la valeur d'usage :
    /// plus k est grand, moins le premier rang d'une seule des deux recherches suffit à dominer.
    /// </summary>
    public int RrfK { get; set; } = 60;
}

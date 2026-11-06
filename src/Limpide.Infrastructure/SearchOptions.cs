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

    /// <summary>
    /// Renvois suivis (ancre trouvée → ancre ajoutée) : quand un passage de la clé remonte, tous les passages de la valeur
    /// sont ajoutés après les résultats, dans l'ordre du texte. L'article 6 renvoie à l'annexe III, dont les extraits
    /// utiles ne remontent pas toujours : q03, « tri de CV », 2026-11-06. Vide par défaut.
    /// </summary>
    public Dictionary<string, string> Follow { get; set; } = [];
}

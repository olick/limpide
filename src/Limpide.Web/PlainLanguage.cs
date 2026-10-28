namespace Limpide.Web;

/// <summary>
/// Traductions en langage courant pour le panneau « sous le capot » : un visiteur sans connaissance technique doit
/// comprendre ce qui s'est passé (demande du 2026-10-12, premier retour : « je n'ai pas tout compris »). Les termes
/// techniques restent disponibles dans les détails repliés.
/// </summary>
public static class PlainLanguage
{
    /// <summary>Nom lisible d'un modèle épinglé par version datée (configuration Generation:Model).</summary>
    public static string ModelName(string model) => model switch
    {
        "mistral-medium-2604" => "Mistral Medium 3.5",
        "mistral-small-2603" => "Mistral Small 4",
        _ => model,
    };

    /// <summary>Un garde-fou déclenché : ce qui s'est passé, et ce que le contrôle protège.</summary>
    public static (string Title, string Meaning) Guardrail(string code) => code switch
    {
        "hors-perimetre" => ("Question hors du sujet des textes",
            "Aucun passage ne parle vraiment de votre question. Plutôt que de laisser le modèle inventer une réponse, "
            + "Limpide ne l'a pas appelé."),
        "injection-suspectee" => ("Question adressée aux consignes de l'assistant",
            "Votre question cherche à modifier ou à lire les instructions de l'assistant (ou cite un passage [P…] qu'il "
            + "ne vous a pas montré). Limpide ne l'a ni cherchée dans les textes, ni transmise au modèle de rédaction."),
        "qualification-juridique" => ("La réponse semble juger votre situation",
            "Limpide doit seulement montrer ce que disent les textes, pas dire si vous êtes en règle. "
            + "Fiez-vous aux extraits, et faites apprécier votre cas par un professionnel du droit."),
        "citation-inventee" => ("Une source citée n'existe pas",
            "La réponse renvoie à un passage qui ne fait pas partie des textes fournis : la phrase concernée n'est pas vérifiable."),
        "sans-citation" => ("Réponse sans source",
            "Rien ne rattache la réponse aux textes officiels : ne vous y fiez pas sans vérifier."),
        "reponse-tronquee" => ("Réponse coupée",
            "Le modèle a atteint la longueur maximale autorisée : la fin de la réponse peut manquer."),
        "reponse-ambigue" => ("Réponse hésitante",
            "Le modèle donne des éléments, puis dit ne pas savoir : à lire avec prudence."),
        "reponse-vide" => ("Pas de réponse", "Le modèle n'a rien répondu."),
        "consigne-divulguee" => ("Consignes divulguées",
            "La réponse recopie une partie des instructions données à l'assistant : la question cherchait sans doute à le détourner."),
        _ => (code, ""),
    };

    /// <summary>Coût en euros → « 0,4 centime » ou « 1,2 centime » (ordre de grandeur lisible).</summary>
    public static string Cost(decimal euros, IFormatProvider culture) =>
        euros <= 0 ? "rien" : $"{(euros * 100).ToString("0.##", culture)} centime d'euro";
}

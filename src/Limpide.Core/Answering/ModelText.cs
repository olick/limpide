namespace Limpide.Core.Answering;

/// <summary>Nettoyage du texte renvoyé par le modèle, avant tout contrôle.</summary>
public static class ModelText
{
    // Caractères invisibles : espace de largeur nulle, (non-)liant, gluon de mot, marque d'ordre des octets.
    private static readonly char[] Invisible = ['​', '‌', '‍', '⁠', '﻿'];

    /// <summary>
    /// Retire les caractères invisibles. gpt-oss-120b (mesure du 2026-10-13) écrit « [​P5] » avec une espace de
    /// largeur nulle après le crochet : à l'écran, une citation normale ; pour le contrôle, aucune citation
    /// (garde-fou « sans-citation » à tort). Le texte des sources n'est jamais concerné : seule la réponse du modèle.
    /// </summary>
    public static string Clean(string text) =>
        string.Concat(text.Split(Invisible)).Trim();
}

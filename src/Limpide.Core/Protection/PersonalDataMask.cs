using System.Text.RegularExpressions;

namespace Limpide.Core.Protection;

/// <param name="Text">Texte où chaque donnée repérée est remplacée par son type entre crochets.</param>
/// <param name="Masked">Types masqués (constantes de <see cref="PersonalDataMask"/>), une fois chacun.</param>
public sealed record MaskedText(string Text, IReadOnlyList<string> Masked);

/// <summary>
/// Masque les données personnelles évidentes d'un texte libre avant qu'il soit conservé (questions des visiteurs,
/// S8, session 5, ADR-019) : adresses électroniques, IBAN, numéros de sécurité sociale, numéros de téléphone.
/// Filtre de dernier recours, par motifs : il ne repère ni les noms ni les adresses postales, ce que la mention
/// d'information dit au visiteur.
/// </summary>
public static partial class PersonalDataMask
{
    public const string Email = "adresse-electronique";
    public const string Iban = "iban";
    public const string SocialSecurityNumber = "numero-de-securite-sociale";
    public const string Phone = "telephone";

    // Ordre : du motif le plus long au plus court, pour qu'un IBAN ne soit pas pris pour un numéro de téléphone.
    private static readonly (string Kind, string Label, Regex Pattern)[] Rules =
    [
        (Email, "[adresse électronique]", EmailPattern()),
        (Iban, "[IBAN]", IbanPattern()),
        (SocialSecurityNumber, "[numéro de sécurité sociale]", SocialSecurityPattern()),
        (Phone, "[téléphone]", PhonePattern()),
    ];

    public static MaskedText Mask(string text)
    {
        var masked = new List<string>();
        foreach (var (kind, label, pattern) in Rules)
        {
            var replaced = pattern.Replace(text, label);
            if (replaced != text)
                masked.Add(kind);
            text = replaced;
        }

        return new MaskedText(text, masked);
    }

    [GeneratedRegex(@"[\p{L}0-9._%+-]+@[\p{L}0-9-]+(?:\.[\p{L}0-9-]+)*\.\p{L}{2,}")]
    private static partial Regex EmailPattern();

    // Deux lettres, deux chiffres, puis 11 à 30 caractères par groupes de 4 (FR76 3000 6000 0112 3456 7890 189).
    // Majuscules seulement : en minuscules, des mots ordinaires suivis de chiffres seraient pris pour un IBAN.
    [GeneratedRegex(@"\b[A-Z]{2}\d{2}(?:[ ]?[A-Z0-9]{4}){2,7}(?:[ ]?[A-Z0-9]{1,3})?\b")]
    private static partial Regex IbanPattern();

    // Sexe, année, mois, département (dont 2A, 2B), commune, rang, clé facultative : 1 85 05 78 006 084 36.
    [GeneratedRegex(@"\b[12][ .]?\d{2}[ .]?(?:0[1-9]|1[0-2]|[2-9]\d)[ .]?(?:\d{2}|2[AB])[ .]?\d{3}[ .]?\d{3}(?:[ .]?\d{2})?\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex SocialSecurityPattern();

    // France (06 12 34 56 78, +33 6 12 34 56 78, 0033…) et international (+ suivi de 8 à 15 chiffres).
    [GeneratedRegex(@"(?:(?:\+|00)33[ .-]?(?:\(0\)[ .-]?)?|\b0)[1-9](?:[ .-]?\d{2}){4}\b|\+\d(?:[ .-]?\d){7,14}\b")]
    private static partial Regex PhonePattern();
}

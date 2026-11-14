namespace GestionMagasin.Domain.Common;

/// <summary>
/// Codes-barres EAN-13 attribués par le magasin à ses articles.
///
/// Le préfixe « 20 » appartient à la plage que GS1 réserve à l'usage interne
/// des magasins (diffusion restreinte, de 20 à 29) : aucun article du
/// commerce ne porte un code qui commence ainsi. Un code du magasin ne peut
/// donc jamais se confondre avec le code-barres d'origine d'un vêtement de
/// marque, que le magasin peut toujours saisir à la place.
///
/// L'EAN-13 est le format que toutes les douchettes lisent sans réglage, et
/// il tient sur une petite étiquette de vêtement.
/// </summary>
public static class CodesBarres
{
    /// <summary>Préfixe des codes attribués par le magasin.</summary>
    public const string PrefixeMagasin = "20";

    /// <summary>Plus grand numéro d'article que peut porter un code du magasin.</summary>
    public const long NumeroMaximal = 9_999_999_999;

    /// <summary>
    /// Code du magasin pour le numéro donné : « 20 », le numéro sur dix
    /// chiffres, puis la clé de contrôle.
    /// </summary>
    public static string ComposerCodeMagasin(long numero)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(numero, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(numero, NumeroMaximal);

        var douze = PrefixeMagasin + numero.ToString("D10", System.Globalization.CultureInfo.InvariantCulture);

        return douze + Cle(douze);
    }

    /// <summary>
    /// Clé de contrôle d'un EAN-13 : les chiffres de rang pair, en partant de
    /// la gauche, comptent triple. La douchette refuse un code dont la clé ne
    /// correspond pas — une erreur de lecture ne fait donc jamais vendre un
    /// autre article.
    /// </summary>
    /// <param name="douzeChiffres">Les douze premiers chiffres du code.</param>
    public static char Cle(string douzeChiffres)
    {
        if (douzeChiffres is not { Length: 12 } || !douzeChiffres.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("Douze chiffres sont attendus.", nameof(douzeChiffres));
        }

        var somme = 0;

        for (var i = 0; i < 12; i++)
        {
            var chiffre = douzeChiffres[i] - '0';
            somme += i % 2 == 0 ? chiffre : chiffre * 3;
        }

        return (char)('0' + (10 - somme % 10) % 10);
    }

    /// <summary>Vrai pour un EAN-13 complet dont la clé de contrôle est juste.</summary>
    public static bool EstEan13(string? code) =>
        code is { Length: 13 }
        && code.All(char.IsAsciiDigit)
        && Cle(code[..12]) == code[12];

    /// <summary>Vrai pour un code attribué par le magasin.</summary>
    public static bool EstCodeMagasin(string? code) =>
        EstEan13(code) && code!.StartsWith(PrefixeMagasin, StringComparison.Ordinal);
}

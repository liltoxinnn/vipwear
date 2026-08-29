using System.Globalization;

namespace GestionMagasin.Application.Common;

/// <summary>
/// Version du logiciel, telle qu'elle s'affiche dans le menu.
///
/// Elle répond à une question qui revient à chaque livraison : « est-ce bien
/// la nouvelle version qui tourne ? » Sans repère à l'écran, la seule façon
/// de le savoir était de chercher une modification et de constater son
/// absence — ce qui ne distingue pas un programme périmé d'une modification
/// ratée.
///
/// Le numéro de version ne suffit pas : il reste à 1.0.0 d'une livraison à
/// l'autre. C'est l'horodatage de compilation qui change à chaque fois, et
/// c'est donc lui qui tranche.
/// </summary>
public static class Edition
{
    /// <summary>
    /// Met en forme la version issue de l'assemblage.
    ///
    /// .NET écrit « 1.0.0+20260829-0312 » : le numéro, puis l'horodatage de
    /// compilation que le fichier projet y ajoute. Le tout est réduit à
    /// « v1.0.0 — 29/08 03:12 », lisible d'un coup d'œil dans le menu.
    /// </summary>
    /// <param name="versionAssemblage">
    /// Version informationnelle de l'assemblage, éventuellement absente.
    /// </param>
    public static string Decrire(string? versionAssemblage)
    {
        if (string.IsNullOrWhiteSpace(versionAssemblage))
        {
            return string.Empty;
        }

        var morceaux = versionAssemblage.Split('+', 2);
        var numero = "v" + morceaux[0].Trim();

        if (morceaux.Length == 1)
        {
            return numero;
        }

        var horodatage = morceaux[1].Trim();

        return DateTime.TryParseExact(
            horodatage,
            "yyyyMMdd-HHmm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var moment)
            ? $"{numero} — {moment:dd/MM HH:mm}"
            // Horodatage d'une forme inattendue : mieux vaut le montrer tel
            // quel que de le taire, il reste un repère.
            : $"{numero} — {horodatage}";
    }
}

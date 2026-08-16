using System.Globalization;

namespace GestionMagasin.Application.Common;

/// <summary>
/// Mise en forme des montants et des dates selon les usages français.
/// Le séparateur décimal est la virgule et les milliers sont espacés :
/// « 12 500,00 DA ».
/// </summary>
public static class FormatageMontant
{
    /// <summary>Culture utilisée dans toute l'application, y compris les documents imprimés.</summary>
    public static CultureInfo CultureApplication { get; } = ConstruireCulture();

    public const string SymboleParDefaut = "DA";

    /// <summary>Formate un montant avec son symbole de devise.</summary>
    public static string Formater(decimal montant, string? symboleDevise = null) =>
        $"{montant.ToString("N2", CultureApplication)} {symboleDevise ?? SymboleParDefaut}";

    /// <summary>Formate un montant sans symbole, pour les colonnes de tableaux.</summary>
    public static string FormaterSansDevise(decimal montant) =>
        montant.ToString("N2", CultureApplication);

    /// <summary>Formate une quantité entière.</summary>
    public static string FormaterQuantite(int quantite) =>
        quantite.ToString("N0", CultureApplication);

    /// <summary>Formate une date en heure locale, au format jour/mois/année.</summary>
    public static string FormaterDate(DateTime dateUtc) =>
        VersLocal(dateUtc).ToString("dd/MM/yyyy", CultureApplication);

    /// <summary>Formate une date avec l'heure, en heure locale.</summary>
    public static string FormaterDateHeure(DateTime dateUtc) =>
        VersLocal(dateUtc).ToString("dd/MM/yyyy à HH:mm", CultureApplication);

    /// <summary>Convertit une date universelle en heure locale du magasin.</summary>
    public static DateTime VersLocal(DateTime date) =>
        date.Kind == DateTimeKind.Utc ? date.ToLocalTime() : date;

    private static CultureInfo ConstruireCulture()
    {
        // La culture française est prise comme base puis ajustée : l'espace
        // insécable des milliers est remplacé par une espace simple, mieux
        // rendue par les imprimantes de tickets.
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("fr-FR").Clone();

        culture.NumberFormat.NumberGroupSeparator = " ";
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.CurrencyGroupSeparator = " ";
        culture.NumberFormat.CurrencyDecimalSeparator = ",";

        return culture;
    }
}

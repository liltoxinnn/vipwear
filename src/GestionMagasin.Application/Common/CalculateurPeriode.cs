using System.Globalization;
using GestionMagasin.Application.DTOs;

namespace GestionMagasin.Application.Common;

/// <summary>Granularités proposées dans les filtres de rapports.</summary>
public enum TypePeriode
{
    Jour,
    Semaine,
    Mois,
    Annee,
    Personnalisee
}

/// <summary>
/// Convertit une période choisie par l'utilisateur (« ce mois-ci ») en bornes
/// exactes en heure universelle. Les dates sont saisies et affichées en heure
/// locale du magasin, mais stockées en UTC : cette conversion est faite ici,
/// une fois pour toutes, plutôt que dans chaque rapport.
/// </summary>
public static class CalculateurPeriode
{
    /// <summary>
    /// Les libellés sont mis en forme avec la culture du logiciel, jamais avec
    /// celle du poste : sur un Windows configuré en anglais, « MMMM » donnerait
    /// « August » au lieu de « août » au milieu d'une phrase française.
    /// </summary>
    private static CultureInfo Culture => FormatageMontant.CultureApplication;

    public static Periode Construire(
        TypePeriode type,
        DateTime referenceLocale,
        DateTime? debutPersonnalise = null,
        DateTime? finPersonnalisee = null)
    {
        return type switch
        {
            TypePeriode.Jour => Depuis(
                referenceLocale.Date,
                referenceLocale.Date.AddDays(1),
                string.Format(Culture, "Journée du {0:dd/MM/yyyy}", referenceLocale)),

            TypePeriode.Semaine => ConstruireSemaine(referenceLocale),

            TypePeriode.Mois => Depuis(
                new DateTime(referenceLocale.Year, referenceLocale.Month, 1, 0, 0, 0, DateTimeKind.Local),
                new DateTime(referenceLocale.Year, referenceLocale.Month, 1, 0, 0, 0, DateTimeKind.Local).AddMonths(1),
                string.Format(Culture, "Mois de {0:MMMM yyyy}", referenceLocale)),

            TypePeriode.Annee => Depuis(
                new DateTime(referenceLocale.Year, 1, 1, 0, 0, 0, DateTimeKind.Local),
                new DateTime(referenceLocale.Year + 1, 1, 1, 0, 0, 0, DateTimeKind.Local),
                string.Format(Culture, "Année {0:yyyy}", referenceLocale)),

            TypePeriode.Personnalisee => ConstruirePersonnalisee(debutPersonnalise, finPersonnalisee),

            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Période inconnue.")
        };
    }

    /// <summary>Journée en cours.</summary>
    public static Periode Aujourdhui(DateTime referenceLocale) =>
        Construire(TypePeriode.Jour, referenceLocale);

    /// <summary>Mois en cours.</summary>
    public static Periode MoisEnCours(DateTime referenceLocale) =>
        Construire(TypePeriode.Mois, referenceLocale);

    /// <summary>Fenêtre glissante des N derniers jours, jour en cours inclus.</summary>
    public static Periode DerniersJours(DateTime referenceLocale, int nombreJours)
    {
        var fin = referenceLocale.Date.AddDays(1);
        var debut = fin.AddDays(-nombreJours);

        return Depuis(debut, fin, string.Format(Culture, "{0} derniers jours", nombreJours));
    }

    /// <summary>Fenêtre glissante des N derniers mois, mois en cours inclus.</summary>
    public static Periode DerniersMois(DateTime referenceLocale, int nombreMois)
    {
        var debutMoisCourant = new DateTime(referenceLocale.Year, referenceLocale.Month, 1, 0, 0, 0, DateTimeKind.Local);
        var fin = debutMoisCourant.AddMonths(1);
        var debut = debutMoisCourant.AddMonths(-(nombreMois - 1));

        return Depuis(debut, fin, string.Format(Culture, "{0} derniers mois", nombreMois));
    }

    private static Periode ConstruireSemaine(DateTime referenceLocale)
    {
        // La semaine commence le lundi, usage courant en Algérie comme en France.
        var decalage = ((int)referenceLocale.DayOfWeek + 6) % 7;
        var debut = referenceLocale.Date.AddDays(-decalage);
        var fin = debut.AddDays(7);

        return Depuis(debut, fin, string.Format(Culture, "Semaine du {0:dd/MM/yyyy} au {1:dd/MM/yyyy}", debut, fin.AddDays(-1)));
    }

    private static Periode ConstruirePersonnalisee(DateTime? debut, DateTime? fin)
    {
        if (!debut.HasValue || !fin.HasValue)
        {
            throw new Domain.Exceptions.ValidationMetierException(
                "Veuillez indiquer une date de début et une date de fin.");
        }

        if (fin.Value.Date < debut.Value.Date)
        {
            throw new Domain.Exceptions.ValidationMetierException(
                "La date de fin doit être postérieure à la date de début.");
        }

        // La borne de fin est portée à la fin de journée pour que les ventes
        // du dernier jour soient bien comptées.
        return Depuis(
            debut.Value.Date,
            fin.Value.Date.AddDays(1),
            string.Format(Culture, "Du {0:dd/MM/yyyy} au {1:dd/MM/yyyy}", debut.Value, fin.Value));
    }

    private static Periode Depuis(DateTime debutLocal, DateTime finLocale, string libelle) => new()
    {
        DebutUtc = DateTime.SpecifyKind(debutLocal, DateTimeKind.Local).ToUniversalTime(),
        FinUtc = DateTime.SpecifyKind(finLocale, DateTimeKind.Local).ToUniversalTime(),
        Libelle = libelle
    };
}

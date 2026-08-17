using System.Globalization;
using GestionMagasin.Application.Common;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Les libellés de période sont affichés tels quels dans les rapports, les
/// exports et les documents imprimés. Ils doivent rester en français même
/// lorsque Windows est configuré dans une autre langue, ce qui est fréquent
/// sur les postes vendus en Algérie.
///
/// Ces tests n'ont pas besoin de base de données : ils portent uniquement sur
/// le calcul des bornes et la rédaction des libellés.
/// </summary>
public class TestsLibellesPeriodes
{
    /// <summary>Exécute une action en simulant un poste configuré en anglais.</summary>
    private static T SousCulture<T>(string culture, Func<T> action)
    {
        var precedente = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);

            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = precedente;
        }
    }

    private static readonly DateTime Reference = new(2026, 8, 17, 18, 38, 0, DateTimeKind.Local);

    [Theory]
    [InlineData("en-US")]
    [InlineData("ar-DZ")]
    [InlineData("fr-FR")]
    public void Le_libelle_du_mois_reste_en_francais(string cultureDuPoste)
    {
        var periode = SousCulture(cultureDuPoste,
            () => CalculateurPeriode.Construire(TypePeriode.Mois, Reference));

        Assert.Equal("Mois de août 2026", periode.Libelle);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("ar-DZ")]
    public void Les_dates_des_libelles_restent_au_format_francais(string cultureDuPoste)
    {
        var jour = SousCulture(cultureDuPoste,
            () => CalculateurPeriode.Construire(TypePeriode.Jour, Reference));

        Assert.Equal("Journée du 17/08/2026", jour.Libelle);

        var semaine = SousCulture(cultureDuPoste,
            () => CalculateurPeriode.Construire(TypePeriode.Semaine, Reference));

        // 17 août 2026 est un lundi : la semaine va du 17 au 23.
        Assert.Equal("Semaine du 17/08/2026 au 23/08/2026", semaine.Libelle);

        var personnalisee = SousCulture(cultureDuPoste,
            () => CalculateurPeriode.Construire(
                TypePeriode.Personnalisee,
                Reference,
                new DateTime(2026, 1, 5),
                new DateTime(2026, 3, 9)));

        Assert.Equal("Du 05/01/2026 au 09/03/2026", personnalisee.Libelle);
    }

    [Fact]
    public void Le_libelle_de_l_annee_reste_en_francais()
    {
        var periode = SousCulture("en-US",
            () => CalculateurPeriode.Construire(TypePeriode.Annee, Reference));

        Assert.Equal("Année 2026", periode.Libelle);
    }

    [Fact]
    public void Les_fenetres_glissantes_sont_libellees_en_francais()
    {
        var jours = SousCulture("en-US", () => CalculateurPeriode.DerniersJours(Reference, 30));
        var mois = SousCulture("en-US", () => CalculateurPeriode.DerniersMois(Reference, 12));

        Assert.Equal("30 derniers jours", jours.Libelle);
        Assert.Equal("12 derniers mois", mois.Libelle);
    }

    [Fact]
    public void La_culture_du_poste_ne_modifie_pas_les_bornes_calculees()
    {
        var enFrancais = SousCulture("fr-FR",
            () => CalculateurPeriode.Construire(TypePeriode.Mois, Reference));

        var enAnglais = SousCulture("en-US",
            () => CalculateurPeriode.Construire(TypePeriode.Mois, Reference));

        Assert.Equal(enFrancais.DebutUtc, enAnglais.DebutUtc);
        Assert.Equal(enFrancais.FinUtc, enAnglais.FinUtc);
    }
}

using GestionMagasin.Application.Services;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Saisons proposées à la saisie d'un produit.
///
/// La liste était bâtie sur les seules saisons déjà employées : un magasin
/// qui vient d'ouvrir n'avait donc rien à choisir, et chacun inventait son
/// orthographe. Le filtre par saison ne regroupait alors plus rien.
/// </summary>
public class TestsSaisons
{
    [Fact]
    public void Un_catalogue_vide_propose_deja_les_quatre_saisons()
    {
        var proposees = Saisons.Composer([]);

        Assert.Equal(["Printemps", "Été", "Automne", "Hiver"], proposees);
    }

    [Fact]
    public void Les_quatre_saisons_restent_dans_l_ordre_du_calendrier()
    {
        // Un tri alphabétique donnerait Automne, Été, Hiver, Printemps :
        // l'ordre du calendrier est le seul que le vendeur reconnaisse.
        var proposees = Saisons.Composer(["Hiver", "Printemps"]);

        Assert.Equal(["Printemps", "Été", "Automne", "Hiver"], proposees);
    }

    [Fact]
    public void Une_periode_propre_au_magasin_rejoint_la_liste_apres_les_saisons()
    {
        var proposees = Saisons.Composer(["Ramadan", "Aïd"]);

        Assert.Equal(["Printemps", "Été", "Automne", "Hiver", "Aïd", "Ramadan"], proposees);
    }

    [Fact]
    public void Une_saison_deja_saisie_sans_accent_ne_cree_pas_de_doublon()
    {
        // « ete » tapé à la va-vite désigne l'été : en faire une cinquième
        // saison couperait le catalogue en deux dans les rapports.
        var proposees = Saisons.Composer(["ete", "ÉTÉ", "hiver"]);

        Assert.Equal(["Printemps", "Été", "Automne", "Hiver"], proposees);
    }

    [Fact]
    public void Les_valeurs_vides_du_catalogue_sont_ignorees()
    {
        var proposees = Saisons.Composer(["", "   ", "Ramadan"]);

        Assert.Equal(["Printemps", "Été", "Automne", "Hiver", "Ramadan"], proposees);
    }

    [Fact]
    public void Une_periode_repetee_n_apparait_qu_une_fois()
    {
        var proposees = Saisons.Composer(["Ramadan", "ramadan", " Ramadan "]);

        Assert.Equal(["Printemps", "Été", "Automne", "Hiver", "Ramadan"], proposees);
    }
}

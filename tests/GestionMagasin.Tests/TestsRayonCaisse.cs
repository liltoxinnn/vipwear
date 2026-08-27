using GestionMagasin.Application.DTOs;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Regroupement du catalogue tel qu'il s'affiche en caisse.
///
/// Le stock se compte par déclinaison, mais le client demande un produit. La
/// caisse montre donc un produit par vignette, puis la couleur et la taille.
/// Un mauvais regroupement — tailles dans le désordre, stock mal cumulé —
/// conduirait le caissier à annoncer une disponibilité fausse au comptoir.
/// </summary>
public class TestsRayonCaisse
{
    private static VarianteDto Declinaison(
        int id,
        int produitId,
        string produit,
        int couleurId,
        string couleur,
        string taille,
        int ordreTaille,
        int stock,
        decimal prix = 1400m) =>
        new()
        {
            Id = id,
            ProduitId = produitId,
            ProduitNom = produit,
            CouleurId = couleurId,
            Couleur = couleur,
            Taille = taille,
            OrdreTaille = ordreTaille,
            QuantiteDisponible = stock,
            PrixVente = prix,
            Sku = $"{produitId}-{couleurId}-{taille}"
        };

    [Fact]
    public void Les_declinaisons_d_un_produit_tiennent_dans_une_seule_vignette()
    {
        var rayon = ArticleRayon.Regrouper(
        [
            Declinaison(1, 10, "pantalon", 1, "Bleu", "S", 2, 40),
            Declinaison(2, 10, "pantalon", 1, "Bleu", "M", 3, 20),
            Declinaison(3, 10, "pantalon", 2, "Gris", "M", 3, 5),
            Declinaison(4, 20, "chemise", 1, "Bleu", "L", 4, 7)
        ]);

        Assert.Equal(2, rayon.Count);

        var pantalon = rayon.Single(a => a.Nom == "pantalon");

        Assert.Equal(2, pantalon.Couleurs.Count);
        Assert.Equal(3, pantalon.NombreDeclinaisons);

        // 40 + 20 + 5 : le caissier lit le stock du produit entier.
        Assert.Equal(65, pantalon.StockTotal);
    }

    [Fact]
    public void Les_tailles_sont_presentees_de_la_plus_petite_a_la_plus_grande()
    {
        var rayon = ArticleRayon.Regrouper(
        [
            Declinaison(1, 10, "pantalon", 1, "Bleu", "XL", 5, 3),
            Declinaison(2, 10, "pantalon", 1, "Bleu", "S", 2, 4),
            Declinaison(3, 10, "pantalon", 1, "Bleu", "M", 3, 5)
        ]);

        var bleu = Assert.Single(rayon[0].Couleurs);

        Assert.Equal(["S", "M", "XL"], bleu.Tailles.Select(t => t.Taille));
        Assert.Equal(12, bleu.StockTotal);
    }

    [Fact]
    public void Une_couleur_entierement_epuisee_reste_affichee()
    {
        var rayon = ArticleRayon.Regrouper(
        [
            Declinaison(1, 10, "pantalon", 1, "Bleu", "S", 2, 4),
            Declinaison(2, 10, "pantalon", 2, "Noir", "S", 2, 0)
        ]);

        var noir = rayon[0].Couleurs.Single(c => c.Nom == "Noir");

        // La taille manquante doit se voir : le caissier saura quoi commander,
        // et ce que le client demandait.
        Assert.Equal(0, noir.StockTotal);
        Assert.True(noir.Tailles[0].EnRupture);
    }

    [Fact]
    public void Un_produit_sans_choix_possible_est_ajoute_directement()
    {
        var seule = ArticleRayon.Regrouper(
            [Declinaison(1, 10, "ceinture", 1, "Noir", "Unique", 1, 6)])[0];

        Assert.True(seule.SansChoix);
        Assert.Equal(1, seule.Declinaison.Id);

        var multiple = ArticleRayon.Regrouper(
        [
            Declinaison(1, 10, "pantalon", 1, "Bleu", "S", 2, 4),
            Declinaison(2, 10, "pantalon", 1, "Bleu", "M", 3, 4)
        ])[0];

        Assert.False(multiple.SansChoix);
    }

    /// <summary>
    /// La vignette n'affiche couleurs et tailles que si elles y tiennent.
    /// Une chemise en cinq couleurs et six tailles ferait trente cases sur
    /// une surface large comme la main : illisibles, et impossibles à viser
    /// au doigt. Elle s'ouvre alors dans un panneau.
    /// </summary>
    [Fact]
    public void Le_choix_reste_sur_la_vignette_tant_qu_il_y_tient()
    {
        static IReadOnlyList<ArticleRayon> Produit(int couleurs, int tailles)
        {
            var declinaisons = new List<VarianteDto>();
            var id = 1;

            for (var c = 1; c <= couleurs; c++)
            {
                for (var t = 1; t <= tailles; t++)
                {
                    declinaisons.Add(Declinaison(id++, 10, "article", c, $"C{c}", $"T{t}", t, 5));
                }
            }

            return ArticleRayon.Regrouper(declinaisons);
        }

        // Le cas courant d'un magasin : quelques couleurs, quelques tailles.
        Assert.True(Produit(2, 3)[0].ChoixSurLaVignette);
        Assert.True(Produit(4, 6)[0].ChoixSurLaVignette);

        // Au-delà, la vignette déborderait.
        Assert.False(Produit(5, 6)[0].ChoixSurLaVignette);
        Assert.False(Produit(4, 7)[0].ChoixSurLaVignette);

        // Une seule déclinaison : rien à choisir du tout.
        Assert.False(Produit(1, 1)[0].ChoixSurLaVignette);
        Assert.True(Produit(1, 1)[0].SansChoix);
    }

    [Fact]
    public void Le_prix_annonce_est_le_plus_bas_lorsque_les_declinaisons_different()
    {
        var uniforme = ArticleRayon.Regrouper(
        [
            Declinaison(1, 10, "pantalon", 1, "Bleu", "S", 2, 4, 1400m),
            Declinaison(2, 10, "pantalon", 1, "Bleu", "M", 3, 4, 1400m)
        ])[0];

        Assert.True(uniforme.PrixUnique);
        Assert.Equal(1400m, uniforme.PrixMinimum);

        var varie = ArticleRayon.Regrouper(
        [
            Declinaison(1, 10, "pantalon", 1, "Bleu", "S", 2, 4, 1400m),
            Declinaison(2, 10, "pantalon", 1, "Bleu", "XXL", 7, 4, 1600m)
        ])[0];

        Assert.False(varie.PrixUnique);
        Assert.Equal(1400m, varie.PrixMinimum);
        Assert.Equal(1600m, varie.PrixMaximum);
    }

    [Fact]
    public void Le_resume_annonce_le_nombre_de_couleurs_et_de_tailles()
    {
        var varie = ArticleRayon.Regrouper(
        [
            Declinaison(1, 10, "pantalon", 1, "Bleu", "S", 2, 4),
            Declinaison(2, 10, "pantalon", 1, "Bleu", "M", 3, 4),
            Declinaison(3, 10, "pantalon", 2, "Gris", "S", 2, 4)
        ])[0];

        Assert.Equal("2 couleurs · 2 tailles", varie.Resume);

        // Une seule couleur et une seule taille : les nommer vaut mieux que
        // d'annoncer « 1 couleur · 1 taille ».
        var seule = ArticleRayon.Regrouper(
            [Declinaison(1, 10, "ceinture", 1, "Noir", "Unique", 1, 6)])[0];

        Assert.Equal("Noir · Unique", seule.Resume);
    }
}

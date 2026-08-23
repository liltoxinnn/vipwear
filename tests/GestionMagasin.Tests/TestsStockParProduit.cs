using GestionMagasin.Application.DTOs;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Regroupement de l'écran Stock par produit.
///
/// Le stock se compte par déclinaison, et c'est ainsi qu'il se corrige. Mais
/// la liste s'ouvre sur les produits : un totalisé faux y ferait commander à
/// tort, ou manquer une rupture.
/// </summary>
public class TestsStockParProduit
{
    private static LigneStockDto Declinaison(
        int id,
        int produitId,
        string produit,
        string couleur,
        string taille,
        int stock,
        int seuil = 0,
        decimal prixAchat = 100m) =>
        new()
        {
            VarianteProduitId = id,
            ProduitId = produitId,
            ProduitNom = produit,
            Reference = $"REF{produitId}",
            Couleur = couleur,
            Taille = taille,
            QuantiteDisponible = stock,
            SeuilMinimum = seuil,
            PrixAchat = prixAchat,
            Sku = $"{produitId}-{couleur}-{taille}"
        };

    [Fact]
    public void Le_stock_et_la_valeur_du_produit_sont_la_somme_de_ses_declinaisons()
    {
        var produits = LigneStockProduit.Regrouper(
        [
            Declinaison(1, 10, "pantalon", "Bleu", "S", 40, prixAchat: 1220m),
            Declinaison(2, 10, "pantalon", "Bleu", "M", 20, prixAchat: 1220m),
            Declinaison(3, 10, "pantalon", "Gris", "XS", 40, prixAchat: 1220m),
            Declinaison(4, 20, "chemise", "Bleu", "L", 5, prixAchat: 800m)
        ]);

        Assert.Equal(2, produits.Count);

        var pantalon = produits.Single(p => p.ProduitNom == "pantalon");

        Assert.Equal(100, pantalon.StockTotal);
        Assert.Equal(122000m, pantalon.ValeurStock);
        Assert.Equal(3, pantalon.NombreDeclinaisons);
        Assert.Equal(2, pantalon.NombreCouleurs);
        Assert.Equal(3, pantalon.NombreTailles);
        Assert.Equal("2 couleurs · 3 tailles", pantalon.Resume);
    }

    /// <summary>
    /// Un produit dont il reste une seule taille se vend mal. Une pastille
    /// verte le cacherait derrière un stock total flatteur.
    /// </summary>
    [Fact]
    public void Un_produit_troue_n_est_pas_annonce_disponible()
    {
        var produit = LigneStockProduit.Regrouper(
        [
            Declinaison(1, 10, "pantalon", "Bleu", "S", 40),
            Declinaison(2, 10, "pantalon", "Bleu", "M", 0),
            Declinaison(3, 10, "pantalon", "Gris", "S", 0),
            Declinaison(4, 10, "pantalon", "Gris", "M", 0)
        ])[0];

        Assert.Equal(40, produit.StockTotal);
        Assert.Equal(3, produit.NombreRuptures);
        Assert.Equal("Stock faible", produit.LibelleEtat);
        Assert.Equal("3 ruptures", produit.LibelleAlerte);
    }

    [Fact]
    public void Un_produit_entierement_epuise_est_en_rupture()
    {
        var produit = LigneStockProduit.Regrouper(
        [
            Declinaison(1, 10, "pantalon", "Bleu", "S", 0),
            Declinaison(2, 10, "pantalon", "Bleu", "M", 0)
        ])[0];

        Assert.Equal(0, produit.StockTotal);
        Assert.Equal("Rupture", produit.LibelleEtat);
        Assert.Equal("2 ruptures", produit.LibelleAlerte);
    }

    [Fact]
    public void Un_produit_complet_n_affiche_aucune_alerte()
    {
        var produit = LigneStockProduit.Regrouper(
        [
            Declinaison(1, 10, "pantalon", "Bleu", "S", 40, seuil: 5),
            Declinaison(2, 10, "pantalon", "Bleu", "M", 20, seuil: 5)
        ])[0];

        Assert.Equal("Disponible", produit.LibelleEtat);
        Assert.Equal(string.Empty, produit.LibelleAlerte);
    }

    /// <summary>
    /// Le seuil se règle par déclinaison. Une taille passée dessous doit
    /// remonter jusqu'à la ligne du produit, sinon elle ne sera jamais vue.
    /// </summary>
    [Fact]
    public void Une_taille_sous_son_seuil_alerte_sur_la_ligne_du_produit()
    {
        var produit = LigneStockProduit.Regrouper(
        [
            Declinaison(1, 10, "pantalon", "Bleu", "S", 40, seuil: 5),
            Declinaison(2, 10, "pantalon", "Bleu", "M", 3, seuil: 5),
            Declinaison(3, 10, "pantalon", "Gris", "M", 0, seuil: 5)
        ])[0];

        Assert.Equal(1, produit.NombreStockFaible);
        Assert.Equal(1, produit.NombreRuptures);
        Assert.Equal("Stock faible", produit.LibelleEtat);
        Assert.Equal("1 rupture, 1 sous le seuil", produit.LibelleAlerte);
    }

    [Fact]
    public void Les_produits_sont_classes_par_nom()
    {
        var produits = LigneStockProduit.Regrouper(
        [
            Declinaison(1, 30, "veste", "Noir", "M", 1),
            Declinaison(2, 10, "chemise", "Blanc", "M", 1),
            Declinaison(3, 20, "pantalon", "Bleu", "M", 1)
        ]);

        Assert.Equal(["chemise", "pantalon", "veste"], produits.Select(p => p.ProduitNom));
    }
}

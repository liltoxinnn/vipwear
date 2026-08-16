using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Enums;
using GestionMagasin.Domain.Exceptions;
using GestionMagasin.Tests.Socle;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Section 38 — Stock : ajouter, retirer, ajuster, consulter l'historique.
/// Vérifie surtout qu'aucun chemin ne permet d'obtenir un stock négatif et
/// que chaque variation laisse une trace.
/// </summary>
public class TestsStock : BaseDeTest
{
    private IServiceProduits Produits => Service<IServiceProduits>();
    private IServiceStock Stock => Service<IServiceStock>();

    [Fact]
    public async Task Ajouter_du_stock_augmente_la_quantite_et_cree_un_mouvement()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 10);

        var nouvelle = await Stock.EnregistrerEntreeManuelleAsync(
            variante.Id, 5, TypeMouvementStock.Correction, "Réapprovisionnement manuel");

        Assert.Equal(15, nouvelle);
        Assert.Equal(15, await StockAsync(variante.Id));

        var historique = await Stock.ObtenirHistoriqueAsync(variante.Id);
        var dernier = historique.First();

        Assert.Equal(5, dernier.Quantite);
        Assert.Equal(10, dernier.AncienneQuantite);
        Assert.Equal(15, dernier.NouvelleQuantite);
        Assert.Equal("Réapprovisionnement manuel", dernier.Motif);
        Assert.True(dernier.EstEntree);
    }

    [Fact]
    public async Task Retirer_du_stock_diminue_la_quantite_et_cree_un_mouvement()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 10);

        var nouvelle = await Stock.EnregistrerSortieManuelleAsync(
            variante.Id, 4, TypeMouvementStock.Perte, "Article perdu en réserve");

        Assert.Equal(6, nouvelle);
        Assert.Equal(6, await StockAsync(variante.Id));

        var historique = await Stock.ObtenirHistoriqueAsync(variante.Id);
        var dernier = historique.First();

        Assert.Equal(-4, dernier.Quantite);
        Assert.Equal(TypeMouvementStock.Perte, dernier.TypeMouvement);
        Assert.False(dernier.EstEntree);
    }

    [Fact]
    public async Task Ajuster_le_stock_enregistre_l_ecart_constate()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 10);

        // Inventaire physique : seulement 7 articles trouvés en rayon.
        var nouvelle = await Stock.AjusterStockAsync(variante.Id, 7, "Inventaire physique du 16/08");

        Assert.Equal(7, nouvelle);
        Assert.Equal(7, await StockAsync(variante.Id));

        var historique = await Stock.ObtenirHistoriqueAsync(variante.Id);
        var ajustement = historique.First();

        Assert.Equal(TypeMouvementStock.Ajustement, ajustement.TypeMouvement);
        Assert.Equal(-3, ajustement.Quantite);
        Assert.Equal(10, ajustement.AncienneQuantite);
        Assert.Equal(7, ajustement.NouvelleQuantite);
    }

    [Fact]
    public async Task Ajuster_a_la_valeur_actuelle_ne_cree_aucun_mouvement()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 10);

        var avant = (await Stock.ObtenirHistoriqueAsync(variante.Id)).Count;

        await Stock.AjusterStockAsync(variante.Id, 10, "Inventaire conforme");

        var apres = (await Stock.ObtenirHistoriqueAsync(variante.Id)).Count;

        Assert.Equal(avant, apres);
        Assert.Equal(10, await StockAsync(variante.Id));
    }

    [Fact]
    public async Task Retirer_plus_que_le_stock_disponible_est_refuse()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 3);

        var erreur = await Assert.ThrowsAsync<StockInsuffisantException>(() =>
            Stock.EnregistrerSortieManuelleAsync(
                variante.Id, 5, TypeMouvementStock.Perte, "Tentative de sortie excessive"));

        Assert.Equal(5, erreur.QuantiteDemandee);
        Assert.Equal(3, erreur.QuantiteDisponible);
        Assert.Contains("Stock insuffisant", erreur.Message);

        // Le stock n'a pas bougé : l'opération a été intégralement annulée.
        Assert.Equal(3, await StockAsync(variante.Id));
    }

    [Fact]
    public async Task Un_mouvement_refuse_ne_laisse_aucune_trace()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 2);

        var avant = (await Stock.ObtenirHistoriqueAsync(variante.Id)).Count;

        await Assert.ThrowsAsync<StockInsuffisantException>(() =>
            Stock.EnregistrerSortieManuelleAsync(
                variante.Id, 10, TypeMouvementStock.Perte, "Sortie impossible"));

        var apres = await Stock.ObtenirHistoriqueAsync(variante.Id);

        Assert.Equal(avant, apres.Count);
    }

    [Fact]
    public async Task Un_mouvement_sans_motif_est_refuse()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits);

        await Assert.ThrowsAsync<ValidationMetierException>(() =>
            Stock.EnregistrerSortieManuelleAsync(variante.Id, 1, TypeMouvementStock.Perte, "   "));

        await Assert.ThrowsAsync<ValidationMetierException>(() =>
            Stock.AjusterStockAsync(variante.Id, 5, ""));
    }

    [Fact]
    public async Task Verifier_la_disponibilite_repond_selon_le_stock_reel()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 4);

        Assert.True(await Stock.VerifierDisponibiliteAsync(variante.Id, 4));
        Assert.False(await Stock.VerifierDisponibiliteAsync(variante.Id, 5));

        await Stock.ExigerDisponibiliteAsync(variante.Id, 4);

        await Assert.ThrowsAsync<StockInsuffisantException>(() =>
            Stock.ExigerDisponibiliteAsync(variante.Id, 5));
    }

    [Fact]
    public async Task Les_alertes_de_stock_faible_et_de_rupture_sont_correctes()
    {
        var (_, faible) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, "Pull laine", quantiteInitiale: 10, seuilMinimum: 5);

        var (_, rupture) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, "Veste jean", quantiteInitiale: 2, seuilMinimum: 5);

        // Le premier article passe sous son seuil, le second tombe à zéro.
        await Stock.EnregistrerSortieManuelleAsync(
            faible.Id, 6, TypeMouvementStock.Perte, "Sortie de test");

        await Stock.EnregistrerSortieManuelleAsync(
            rupture.Id, 2, TypeMouvementStock.Perte, "Sortie de test");

        var stockFaible = await Stock.ListerStockFaibleAsync();
        var ruptures = await Stock.ListerRupturesAsync();

        Assert.Contains(stockFaible, l => l.VarianteProduitId == faible.Id);
        Assert.DoesNotContain(stockFaible, l => l.VarianteProduitId == rupture.Id);

        Assert.Contains(ruptures, l => l.VarianteProduitId == rupture.Id);
        Assert.DoesNotContain(ruptures, l => l.VarianteProduitId == faible.Id);
    }

    [Fact]
    public async Task La_valeur_du_stock_est_calculee_au_prix_d_achat()
    {
        await ConstructeurDonnees.CreerArticleAsync(
            Produits, "Article A", prixAchat: 1000m, prixVente: 2000m, quantiteInitiale: 3);

        await ConstructeurDonnees.CreerArticleAsync(
            Produits, "Article B", prixAchat: 500m, prixVente: 1200m, quantiteInitiale: 4);

        var valeur = await Stock.CalculerValeurStockAsync();

        // 3 × 1 000 + 4 × 500 = 5 000 DA
        Assert.Equal(5000m, valeur);
    }

    [Fact]
    public async Task L_historique_peut_etre_filtre_par_type_de_mouvement()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 10);

        await Stock.EnregistrerSortieManuelleAsync(
            variante.Id, 1, TypeMouvementStock.Perte, "Perte constatée");

        await Stock.EnregistrerEntreeManuelleAsync(
            variante.Id, 2, TypeMouvementStock.Correction, "Correction de saisie");

        var pertes = await Stock.ObtenirHistoriqueAsync(
            variante.Id, typeMouvement: TypeMouvementStock.Perte);

        Assert.Single(pertes);
        Assert.Equal(TypeMouvementStock.Perte, pertes[0].TypeMouvement);
    }
}

using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Enums;
using GestionMagasin.Domain.Exceptions;
using GestionMagasin.Tests.Socle;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Section 38 — Achat : créer l'achat, réceptionner, vérifier que le stock a
/// bien augmenté. Le stock ne doit bouger qu'à la réception, jamais à la
/// simple saisie de la commande.
/// </summary>
public class TestsAchats : BaseDeTest
{
    private IServiceProduits Produits => Service<IServiceProduits>();
    private IServiceAchats Achats => Service<IServiceAchats>();
    private IServiceFournisseurs Fournisseurs => Service<IServiceFournisseurs>();
    private IServiceStock Stock => Service<IServiceStock>();

    [Fact]
    public async Task Creer_une_commande_ne_modifie_pas_encore_le_stock()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 5);
        var fournisseur = await ConstructeurDonnees.CreerFournisseurAsync(Fournisseurs);

        var achat = await Achats.CreerAchatAsync(new DemandeAchat
        {
            FournisseurId = fournisseur.Id,
            Lignes = [new LigneAchatDemande { VarianteProduitId = variante.Id, Quantite = 20, PrixUnitaire = 1000m }]
        });

        Assert.Equal(StatutAchat.EnAttente, achat.Statut);
        Assert.Equal(20000m, achat.Total);
        Assert.StartsWith("ACH-", achat.NumeroAchat);

        // La marchandise n'est pas encore arrivée : le stock est inchangé.
        Assert.Equal(5, await StockAsync(variante.Id));
    }

    [Fact]
    public async Task Receptionner_une_commande_augmente_le_stock_et_cree_un_mouvement()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 5);
        var fournisseur = await ConstructeurDonnees.CreerFournisseurAsync(Fournisseurs);

        var achat = await Achats.CreerAchatAsync(new DemandeAchat
        {
            FournisseurId = fournisseur.Id,
            Lignes = [new LigneAchatDemande { VarianteProduitId = variante.Id, Quantite = 20, PrixUnitaire = 1000m }]
        });

        var recu = await Achats.ReceptionnerToutAsync(achat.Id);

        Assert.Equal(StatutAchat.Recu, recu.Statut);
        Assert.NotNull(recu.DateReception);
        Assert.Equal(25, await StockAsync(variante.Id));

        var mouvements = await Stock.ObtenirHistoriqueAsync(variante.Id);
        var reception = mouvements.First(m => m.TypeMouvement == TypeMouvementStock.Achat);

        Assert.Equal(20, reception.Quantite);
        Assert.Equal(5, reception.AncienneQuantite);
        Assert.Equal(25, reception.NouvelleQuantite);
        Assert.Equal(achat.NumeroAchat, reception.ReferenceDocument);
    }

    [Fact]
    public async Task Une_reception_partielle_laisse_la_commande_ouverte()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 0);
        var fournisseur = await ConstructeurDonnees.CreerFournisseurAsync(Fournisseurs);

        var achat = await Achats.CreerAchatAsync(new DemandeAchat
        {
            FournisseurId = fournisseur.Id,
            Lignes = [new LigneAchatDemande { VarianteProduitId = variante.Id, Quantite = 30, PrixUnitaire = 800m }]
        });

        var partiel = await Achats.ReceptionnerAsync(achat.Id,
            [new LigneReception { LigneAchatId = achat.Lignes[0].Id, QuantiteRecue = 12 }]);

        Assert.Equal(StatutAchat.PartiellementRecu, partiel.Statut);
        Assert.Equal(12, partiel.Lignes[0].QuantiteRecue);
        Assert.Equal(18, partiel.Lignes[0].QuantiteRestante);
        Assert.Equal(12, await StockAsync(variante.Id));

        // Le reste arrive plus tard.
        var complet = await Achats.ReceptionnerToutAsync(achat.Id);

        Assert.Equal(StatutAchat.Recu, complet.Statut);
        Assert.Equal(30, await StockAsync(variante.Id));
    }

    [Fact]
    public async Task Receptionner_plus_que_commande_est_refuse()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 0);
        var fournisseur = await ConstructeurDonnees.CreerFournisseurAsync(Fournisseurs);

        var achat = await Achats.CreerAchatAsync(new DemandeAchat
        {
            FournisseurId = fournisseur.Id,
            Lignes = [new LigneAchatDemande { VarianteProduitId = variante.Id, Quantite = 10, PrixUnitaire = 800m }]
        });

        var erreur = await Assert.ThrowsAsync<ValidationMetierException>(() =>
            Achats.ReceptionnerAsync(achat.Id,
                [new LigneReception { LigneAchatId = achat.Lignes[0].Id, QuantiteRecue = 15 }]));

        Assert.Contains("dépasse la quantité restante", erreur.Message);
        Assert.Equal(0, await StockAsync(variante.Id));
    }

    [Fact]
    public async Task Une_reception_immediate_alimente_le_stock_des_la_creation()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 2);
        var fournisseur = await ConstructeurDonnees.CreerFournisseurAsync(Fournisseurs);

        var achat = await Achats.CreerAchatAsync(new DemandeAchat
        {
            FournisseurId = fournisseur.Id,
            ReceptionImmediate = true,
            Lignes = [new LigneAchatDemande { VarianteProduitId = variante.Id, Quantite = 8, PrixUnitaire = 900m }]
        });

        Assert.Equal(StatutAchat.Recu, achat.Statut);
        Assert.Equal(10, await StockAsync(variante.Id));
    }

    [Fact]
    public async Task Une_commande_deja_receptionnee_ne_peut_plus_etre_annulee()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 0);
        var fournisseur = await ConstructeurDonnees.CreerFournisseurAsync(Fournisseurs);

        var achat = await Achats.CreerAchatAsync(new DemandeAchat
        {
            FournisseurId = fournisseur.Id,
            ReceptionImmediate = true,
            Lignes = [new LigneAchatDemande { VarianteProduitId = variante.Id, Quantite = 5, PrixUnitaire = 900m }]
        });

        var erreur = await Assert.ThrowsAsync<ValidationMetierException>(() =>
            Achats.AnnulerAchatAsync(achat.Id, "Changement d'avis"));

        Assert.Contains("déjà rejoint le stock", erreur.Message);
        Assert.Equal(5, await StockAsync(variante.Id));
    }

    [Fact]
    public async Task Une_commande_non_receptionnee_peut_etre_annulee()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 3);
        var fournisseur = await ConstructeurDonnees.CreerFournisseurAsync(Fournisseurs);

        var achat = await Achats.CreerAchatAsync(new DemandeAchat
        {
            FournisseurId = fournisseur.Id,
            Lignes = [new LigneAchatDemande { VarianteProduitId = variante.Id, Quantite = 5, PrixUnitaire = 900m }]
        });

        var annule = await Achats.AnnulerAchatAsync(achat.Id, "Fournisseur en rupture");

        Assert.Equal(StatutAchat.Annule, annule.Statut);
        Assert.Equal(3, await StockAsync(variante.Id));

        await Assert.ThrowsAsync<ValidationMetierException>(() =>
            Achats.ReceptionnerToutAsync(achat.Id));
    }

    [Fact]
    public async Task Les_reglements_fournisseur_reduisent_le_solde_du()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 0);
        var fournisseur = await ConstructeurDonnees.CreerFournisseurAsync(Fournisseurs);

        var achat = await Achats.CreerAchatAsync(new DemandeAchat
        {
            FournisseurId = fournisseur.Id,
            MontantPaye = 4000m,
            Lignes = [new LigneAchatDemande { VarianteProduitId = variante.Id, Quantite = 10, PrixUnitaire = 1000m }]
        });

        Assert.Equal(10000m, achat.Total);
        Assert.Equal(6000m, achat.MontantRestant);

        var apresReglement = await Achats.EnregistrerReglementAsync(achat.Id, 6000m);

        Assert.Equal(10000m, apresReglement.MontantPaye);
        Assert.Equal(0m, apresReglement.MontantRestant);

        // Le solde du fournisseur suit ses commandes.
        var fiche = await Fournisseurs.ObtenirAsync(fournisseur.Id);
        Assert.Equal(0m, fiche!.SoldeDu);
        Assert.Equal(10000m, fiche.TotalCommandes);
    }

    [Fact]
    public async Task Regler_plus_que_le_solde_du_est_refuse()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 0);
        var fournisseur = await ConstructeurDonnees.CreerFournisseurAsync(Fournisseurs);

        var achat = await Achats.CreerAchatAsync(new DemandeAchat
        {
            FournisseurId = fournisseur.Id,
            Lignes = [new LigneAchatDemande { VarianteProduitId = variante.Id, Quantite = 1, PrixUnitaire = 1000m }]
        });

        var erreur = await Assert.ThrowsAsync<ValidationMetierException>(() =>
            Achats.EnregistrerReglementAsync(achat.Id, 5000m));

        Assert.Contains("dépasse le reste à payer", erreur.Message);
    }

    [Fact]
    public async Task Un_fournisseur_desactive_ne_peut_plus_recevoir_de_commande()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 0);
        var fournisseur = await ConstructeurDonnees.CreerFournisseurAsync(Fournisseurs);

        await Fournisseurs.DefinirEtatAsync(fournisseur.Id, actif: false);

        var erreur = await Assert.ThrowsAsync<ValidationMetierException>(() =>
            Achats.CreerAchatAsync(new DemandeAchat
            {
                FournisseurId = fournisseur.Id,
                Lignes = [new LigneAchatDemande { VarianteProduitId = variante.Id, Quantite = 5, PrixUnitaire = 900m }]
            }));

        Assert.Contains("désactivé", erreur.Message);
    }
}

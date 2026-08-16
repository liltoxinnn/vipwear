using GestionMagasin.Application.DTOs;
using Microsoft.EntityFrameworkCore;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Enums;
using GestionMagasin.Domain.Exceptions;
using GestionMagasin.Tests.Socle;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Section 38 — Vente : scanner, panier, paiement, vente, stock diminué.
/// Vérifie la règle centrale du logiciel : une vente ne peut pas exister sans
/// que le stock ait été correctement déduit.
/// </summary>
public class TestsVentes : BaseDeTest
{
    private IServiceProduits Produits => Service<IServiceProduits>();
    private IServiceVentes Ventes => Service<IServiceVentes>();
    private IServiceStock Stock => Service<IServiceStock>();
    private IServiceClients Clients => Service<IServiceClients>();

    [Fact]
    public async Task Une_vente_diminue_le_stock_et_cree_un_mouvement()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 10, prixVente: 2500m);

        var vente = await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 3, 2500m);

        Assert.Equal(7500m, vente.Total);
        Assert.Equal(7500m, vente.MontantPaye);
        Assert.Equal(StatutVente.Payee, vente.Statut);
        Assert.Equal(3, vente.NombreArticles);
        Assert.StartsWith("VTE-", vente.NumeroVente);

        Assert.Equal(7, await StockAsync(variante.Id));

        var mouvements = await Stock.ObtenirHistoriqueAsync(variante.Id);
        var mouvementVente = mouvements.First(m => m.TypeMouvement == TypeMouvementStock.Vente);

        Assert.Equal(-3, mouvementVente.Quantite);
        Assert.Equal(vente.NumeroVente, mouvementVente.ReferenceDocument);
    }

    [Fact]
    public async Task Une_vente_depassant_le_stock_est_refusee_et_ne_laisse_rien()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 2, prixVente: 2500m);

        var erreur = await Assert.ThrowsAsync<StockInsuffisantException>(() =>
            ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 5, 2500m));

        Assert.Equal(5, erreur.QuantiteDemandee);
        Assert.Equal(2, erreur.QuantiteDisponible);

        // Le stock est intact et aucune vente n'a été enregistrée : la
        // transaction a bien été annulée dans son intégralité.
        Assert.Equal(2, await StockAsync(variante.Id));

        var ventes = await Ventes.RechercherAsync();
        Assert.Empty(ventes);

        var nombreLignes = await LireEnBaseAsync(c => c.LignesVente.CountAsync());
        Assert.Equal(0, nombreLignes);
    }

    [Fact]
    public async Task Une_vente_de_plusieurs_articles_est_traitee_en_bloc()
    {
        var (produit, varianteM) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 10, prixVente: 2500m);

        var varianteL = await ConstructeurDonnees.AjouterDeclinaisonAsync(
            Produits, produit.Id, "L", "Blanc", quantiteInitiale: 8);

        var vente = await Ventes.EnregistrerVenteAsync(new DemandeVente
        {
            Lignes =
            [
                new LigneVenteDemande { VarianteProduitId = varianteM.Id, Quantite = 2, PrixUnitaire = 2500m },
                new LigneVenteDemande { VarianteProduitId = varianteL.Id, Quantite = 3, PrixUnitaire = 2500m }
            ],
            Paiements = [new PaiementDemande { ModePaiement = ModePaiement.CarteBancaire, Montant = 12500m }]
        });

        Assert.Equal(12500m, vente.Total);
        Assert.Equal(5, vente.NombreArticles);
        Assert.Equal(8, await StockAsync(varianteM.Id));
        Assert.Equal(5, await StockAsync(varianteL.Id));
    }

    [Fact]
    public async Task Si_un_seul_article_manque_toute_la_vente_est_annulee()
    {
        var (produit, disponible) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 10, prixVente: 2500m);

        var insuffisant = await ConstructeurDonnees.AjouterDeclinaisonAsync(
            Produits, produit.Id, "L", "Blanc", quantiteInitiale: 1);

        await Assert.ThrowsAsync<StockInsuffisantException>(() =>
            Ventes.EnregistrerVenteAsync(new DemandeVente
            {
                Lignes =
                [
                    new LigneVenteDemande { VarianteProduitId = disponible.Id, Quantite = 2, PrixUnitaire = 2500m },
                    new LigneVenteDemande { VarianteProduitId = insuffisant.Id, Quantite = 5, PrixUnitaire = 2500m }
                ],
                Paiements = [new PaiementDemande { ModePaiement = ModePaiement.Especes, Montant = 17500m }]
            }));

        // L'article disponible n'a pas été déduit non plus.
        Assert.Equal(10, await StockAsync(disponible.Id));
        Assert.Equal(1, await StockAsync(insuffisant.Id));
        Assert.Empty(await Ventes.RechercherAsync());
    }

    [Fact]
    public async Task Les_remises_sont_appliquees_au_ticket_et_aux_lignes()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 10, prixVente: 2000m);

        var vente = await Ventes.EnregistrerVenteAsync(new DemandeVente
        {
            RemiseGlobale = 500m,
            Lignes =
            [
                new LigneVenteDemande
                {
                    VarianteProduitId = variante.Id,
                    Quantite = 3,
                    PrixUnitaire = 2000m,
                    Remise = 300m
                }
            ],
            Paiements = [new PaiementDemande { ModePaiement = ModePaiement.Especes, Montant = 5200m }]
        });

        // 3 × 2 000 = 6 000, moins 300 de remise ligne = 5 700 de sous-total,
        // moins 500 de remise ticket = 5 200 à payer.
        Assert.Equal(5700m, vente.SousTotal);
        Assert.Equal(500m, vente.Remise);
        Assert.Equal(5200m, vente.Total);
    }

    [Fact]
    public async Task Une_remise_superieure_au_total_est_refusee()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 10, prixVente: 1000m);

        var erreur = await Assert.ThrowsAsync<ValidationMetierException>(() =>
            Ventes.EnregistrerVenteAsync(new DemandeVente
            {
                RemiseGlobale = 5000m,
                Lignes = [new LigneVenteDemande { VarianteProduitId = variante.Id, Quantite = 1, PrixUnitaire = 1000m }],
                Paiements = []
            }));

        Assert.Contains("dépasse le montant total", erreur.Message);
        Assert.Equal(10, await StockAsync(variante.Id));
    }

    [Fact]
    public async Task Un_reglement_partiel_laisse_la_vente_ouverte()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 10, prixVente: 3000m);

        var vente = await Ventes.EnregistrerVenteAsync(new DemandeVente
        {
            Lignes = [new LigneVenteDemande { VarianteProduitId = variante.Id, Quantite = 2, PrixUnitaire = 3000m }],
            Paiements = [new PaiementDemande { ModePaiement = ModePaiement.Especes, Montant = 4000m }]
        });

        Assert.Equal(StatutVente.PartiellementPayee, vente.Statut);
        Assert.Equal(2000m, vente.MontantRestant);

        var soldee = await Ventes.AjouterPaiementAsync(vente.Id, new PaiementDemande
        {
            ModePaiement = ModePaiement.CarteBancaire,
            Montant = 2000m
        });

        Assert.Equal(StatutVente.Payee, soldee.Statut);
        Assert.Equal(0m, soldee.MontantRestant);
        Assert.Equal(2, soldee.Paiements.Count);
    }

    [Fact]
    public async Task Un_reglement_superieur_au_montant_du_est_refuse()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 10, prixVente: 1000m);

        var erreur = await Assert.ThrowsAsync<ValidationMetierException>(() =>
            Ventes.EnregistrerVenteAsync(new DemandeVente
            {
                Lignes = [new LigneVenteDemande { VarianteProduitId = variante.Id, Quantite = 1, PrixUnitaire = 1000m }],
                Paiements = [new PaiementDemande { ModePaiement = ModePaiement.Especes, Montant = 5000m }]
            }));

        Assert.Contains("dépasse le montant dû", erreur.Message);
    }

    [Fact]
    public async Task Plusieurs_modes_de_paiement_sont_acceptes_sur_une_meme_vente()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 10, prixVente: 5000m);

        var vente = await Ventes.EnregistrerVenteAsync(new DemandeVente
        {
            Lignes = [new LigneVenteDemande { VarianteProduitId = variante.Id, Quantite = 1, PrixUnitaire = 5000m }],
            Paiements =
            [
                new PaiementDemande { ModePaiement = ModePaiement.Especes, Montant = 2000m },
                new PaiementDemande { ModePaiement = ModePaiement.CarteBancaire, Montant = 3000m }
            ]
        });

        Assert.Equal(StatutVente.Payee, vente.Statut);
        Assert.Equal(2, vente.Paiements.Count);
        Assert.Equal(5000m, vente.Paiements.Sum(p => p.Montant));
    }

    [Fact]
    public async Task Une_vente_annulee_reintegre_le_stock()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 10, prixVente: 2000m);

        var vente = await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 4, 2000m);
        Assert.Equal(6, await StockAsync(variante.Id));

        var annulee = await Ventes.AnnulerVenteAsync(vente.Id, "Erreur de caisse");

        Assert.Equal(StatutVente.Annulee, annulee.Statut);
        Assert.Equal(10, await StockAsync(variante.Id));

        // La vente reste dans l'historique, elle n'est jamais supprimée.
        var retrouvee = await Ventes.ObtenirAsync(vente.Id);
        Assert.NotNull(retrouvee);
        Assert.Contains("Erreur de caisse", retrouvee.Notes);
    }

    [Fact]
    public async Task Une_vente_deja_annulee_ne_peut_pas_l_etre_deux_fois()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 10);

        var vente = await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 2, 2500m);
        await Ventes.AnnulerVenteAsync(vente.Id, "Première annulation");

        await Assert.ThrowsAsync<ValidationMetierException>(() =>
            Ventes.AnnulerVenteAsync(vente.Id, "Seconde annulation"));

        // Le stock n'a pas été réintégré deux fois.
        Assert.Equal(10, await StockAsync(variante.Id));
    }

    [Fact]
    public async Task Un_produit_desactive_ne_peut_plus_etre_vendu()
    {
        var (produit, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 10);

        await Produits.DefinirEtatProduitAsync(produit.Id, actif: false);

        var erreur = await Assert.ThrowsAsync<ValidationMetierException>(() =>
            ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 1, 2500m));

        Assert.Contains("n'est plus proposé à la vente", erreur.Message);
        Assert.Equal(10, await StockAsync(variante.Id));
    }

    [Fact]
    public async Task Une_vente_nominative_est_rattachee_au_client()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 10);
        var client = await ConstructeurDonnees.CreerClientAsync(Clients);

        var vente = await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 2, 2500m, client.Id);

        Assert.Equal(client.Id, vente.ClientId);
        Assert.Contains("Karim", vente.ClientNom);

        var historique = await Clients.ObtenirHistoriqueAsync(client.Id);
        Assert.Single(historique);
        Assert.Equal(vente.NumeroVente, historique[0].NumeroVente);

        var fiche = await Clients.ObtenirAsync(client.Id);
        Assert.Equal(1, fiche!.NombreAchats);
        Assert.Equal(5000m, fiche.TotalAchats);
    }

    [Fact]
    public async Task Un_panier_vide_est_refuse()
    {
        var erreur = await Assert.ThrowsAsync<ValidationMetierException>(() =>
            Ventes.EnregistrerVenteAsync(new DemandeVente { Lignes = [], Paiements = [] }));

        Assert.Contains("panier est vide", erreur.Message);
    }

    [Fact]
    public async Task Chaque_vente_recoit_un_numero_unique()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 20);

        var numeros = new List<string>();

        for (var i = 0; i < 5; i++)
        {
            var vente = await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 1, 2500m);
            numeros.Add(vente.NumeroVente);
        }

        Assert.Equal(5, numeros.Distinct().Count());
        Assert.Equal(15, await StockAsync(variante.Id));
    }
}

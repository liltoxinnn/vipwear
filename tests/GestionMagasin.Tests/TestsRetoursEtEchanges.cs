using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Enums;
using GestionMagasin.Domain.Exceptions;
using GestionMagasin.Tests.Socle;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Section 38 — Retour et échange.
/// Règle vérifiée en priorité : un article revendable réintègre le stock,
/// un article endommagé est repris et remboursé mais reste hors du stock.
/// </summary>
public class TestsRetoursEtEchanges : BaseDeTest
{
    private IServiceProduits Produits => Service<IServiceProduits>();
    private IServiceVentes Ventes => Service<IServiceVentes>();
    private IServiceRetours Retours => Service<IServiceRetours>();
    private IServiceStock Stock => Service<IServiceStock>();
    private IServiceClients Clients => Service<IServiceClients>();

    [Fact]
    public async Task Un_article_revendable_reintegre_le_stock()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 10, prixVente: 2500m);

        var vente = await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 3, 2500m);
        Assert.Equal(7, await StockAsync(variante.Id));

        var retour = await Retours.EnregistrerRetourAsync(new DemandeRetour
        {
            VenteId = vente.Id,
            Motif = "Taille inadaptée",
            Lignes =
            [
                new LigneRetourDemande
                {
                    LigneVenteId = vente.Lignes[0].Id,
                    Quantite = 2,
                    EtatArticle = EtatArticleRetour.Revendable
                }
            ]
        });

        Assert.StartsWith("RET-", retour.NumeroRetour);
        Assert.Equal(5000m, retour.TotalRembourse);
        Assert.True(retour.Lignes[0].RemisEnStock);

        // Les deux articles sont revenus en rayon.
        Assert.Equal(9, await StockAsync(variante.Id));

        var mouvements = await Stock.ObtenirHistoriqueAsync(variante.Id);
        var mouvementRetour = mouvements.First(m => m.TypeMouvement == TypeMouvementStock.RetourClient);

        Assert.Equal(2, mouvementRetour.Quantite);
        Assert.Equal(retour.NumeroRetour, mouvementRetour.ReferenceDocument);
    }

    [Fact]
    public async Task Un_article_endommage_est_rembourse_mais_ne_revient_pas_en_stock()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 10, prixVente: 2500m);

        var vente = await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 3, 2500m);
        Assert.Equal(7, await StockAsync(variante.Id));

        var retour = await Retours.EnregistrerRetourAsync(new DemandeRetour
        {
            VenteId = vente.Id,
            Motif = "Article déchiré",
            Lignes =
            [
                new LigneRetourDemande
                {
                    LigneVenteId = vente.Lignes[0].Id,
                    Quantite = 1,
                    EtatArticle = EtatArticleRetour.Endommage
                }
            ]
        });

        Assert.Equal(2500m, retour.TotalRembourse);
        Assert.False(retour.Lignes[0].RemisEnStock);

        // Le stock est resté à 7 : l'article abîmé n'est pas revendable.
        Assert.Equal(7, await StockAsync(variante.Id));
    }

    [Fact]
    public async Task Un_retour_mixte_ne_reintegre_que_les_articles_revendables()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 10, prixVente: 1000m);

        var vente = await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 5, 1000m);
        Assert.Equal(5, await StockAsync(variante.Id));

        var retour = await Retours.EnregistrerRetourAsync(new DemandeRetour
        {
            VenteId = vente.Id,
            Motif = "Retour mixte",
            Lignes =
            [
                new LigneRetourDemande
                {
                    LigneVenteId = vente.Lignes[0].Id,
                    Quantite = 2,
                    EtatArticle = EtatArticleRetour.Revendable
                },
                new LigneRetourDemande
                {
                    LigneVenteId = vente.Lignes[0].Id,
                    Quantite = 1,
                    EtatArticle = EtatArticleRetour.Endommage
                }
            ]
        });

        // Trois articles remboursés, mais seulement deux remis en rayon.
        Assert.Equal(3000m, retour.TotalRembourse);
        Assert.Equal(7, await StockAsync(variante.Id));
    }

    [Fact]
    public async Task Retourner_plus_que_la_quantite_vendue_est_refuse()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 10, prixVente: 2500m);

        var vente = await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 2, 2500m);

        var erreur = await Assert.ThrowsAsync<ValidationMetierException>(() =>
            Retours.EnregistrerRetourAsync(new DemandeRetour
            {
                VenteId = vente.Id,
                Lignes =
                [
                    new LigneRetourDemande { LigneVenteId = vente.Lignes[0].Id, Quantite = 5 }
                ]
            }));

        Assert.Contains("peuvent encore être retournés", erreur.Message);

        // Rien n'a bougé.
        Assert.Equal(8, await StockAsync(variante.Id));
    }

    [Fact]
    public async Task Deux_retours_successifs_ne_depassent_pas_la_quantite_vendue()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 10, prixVente: 2000m);

        var vente = await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 3, 2000m);

        await Retours.EnregistrerRetourAsync(new DemandeRetour
        {
            VenteId = vente.Id,
            Lignes = [new LigneRetourDemande { LigneVenteId = vente.Lignes[0].Id, Quantite = 2 }]
        });

        // Il ne reste qu'un article retournable.
        var erreur = await Assert.ThrowsAsync<ValidationMetierException>(() =>
            Retours.EnregistrerRetourAsync(new DemandeRetour
            {
                VenteId = vente.Id,
                Lignes = [new LigneRetourDemande { LigneVenteId = vente.Lignes[0].Id, Quantite = 2 }]
            }));

        Assert.Contains("seulement 1", erreur.Message);
        Assert.Equal(9, await StockAsync(variante.Id));
    }

    [Fact]
    public async Task Le_statut_de_la_vente_suit_les_retours()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 10, prixVente: 2000m);

        var vente = await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 4, 2000m);

        await Retours.EnregistrerRetourAsync(new DemandeRetour
        {
            VenteId = vente.Id,
            Lignes = [new LigneRetourDemande { LigneVenteId = vente.Lignes[0].Id, Quantite = 1 }]
        });

        var partielle = await Ventes.ObtenirAsync(vente.Id);
        Assert.Equal(StatutVente.PartiellementRetournee, partielle!.Statut);

        await Retours.EnregistrerRetourAsync(new DemandeRetour
        {
            VenteId = vente.Id,
            Lignes = [new LigneRetourDemande { LigneVenteId = vente.Lignes[0].Id, Quantite = 3 }]
        });

        var complete = await Ventes.ObtenirAsync(vente.Id);
        Assert.Equal(StatutVente.Retournee, complete!.Statut);
        Assert.Equal(10, await StockAsync(variante.Id));
    }

    [Fact]
    public async Task Le_remboursement_tient_compte_de_la_remise_accordee()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 10, prixVente: 1000m);

        // Le client paie 3 600 DA au lieu de 4 000 grâce à une remise ticket.
        var vente = await Ventes.EnregistrerVenteAsync(new DemandeVente
        {
            RemiseGlobale = 400m,
            Lignes = [new LigneVenteDemande { VarianteProduitId = variante.Id, Quantite = 4, PrixUnitaire = 1000m }],
            Paiements = [new PaiementDemande { ModePaiement = ModePaiement.Especes, Montant = 3600m }]
        });

        Assert.Equal(3600m, vente.Total);

        var retour = await Retours.EnregistrerRetourAsync(new DemandeRetour
        {
            VenteId = vente.Id,
            Lignes = [new LigneRetourDemande { LigneVenteId = vente.Lignes[0].Id, Quantite = 2 }]
        });

        // Le client est remboursé de ce qu'il a réellement payé : 900 DA
        // l'unité, et non 1 000 DA.
        Assert.Equal(1800m, retour.TotalRembourse);
    }

    [Fact]
    public async Task Une_vente_annulee_ne_peut_pas_faire_l_objet_d_un_retour()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 10, prixVente: 2000m);

        var vente = await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 2, 2000m);
        await Ventes.AnnulerVenteAsync(vente.Id, "Erreur de saisie");

        var erreur = await Assert.ThrowsAsync<ValidationMetierException>(() =>
            Retours.EnregistrerRetourAsync(new DemandeRetour
            {
                VenteId = vente.Id,
                Lignes = [new LigneRetourDemande { LigneVenteId = vente.Lignes[0].Id, Quantite = 1 }]
            }));

        Assert.Contains("annulée", erreur.Message);

        // Le stock reste celui d'après l'annulation, sans double réintégration.
        Assert.Equal(10, await StockAsync(variante.Id));
    }

    [Fact]
    public async Task Un_echange_de_taille_met_correctement_a_jour_les_deux_stocks()
    {
        // Le client a acheté une taille M et souhaite l'échanger contre une L.
        var (produit, tailleM) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 10, prixVente: 3000m);

        var tailleL = await ConstructeurDonnees.AjouterDeclinaisonAsync(
            Produits, produit.Id, "L", "Noir", quantiteInitiale: 6);

        var client = await ConstructeurDonnees.CreerClientAsync(Clients);
        var vente = await ConstructeurDonnees.VendreAsync(Ventes, tailleM.Id, 1, 3000m, client.Id);

        Assert.Equal(9, await StockAsync(tailleM.Id));
        Assert.Equal(6, await StockAsync(tailleL.Id));

        var echange = await Retours.EnregistrerEchangeAsync(new DemandeEchange
        {
            VenteId = vente.Id,
            Motif = "Échange de taille M vers L",
            LignesRetournees =
            [
                new LigneRetourDemande
                {
                    LigneVenteId = vente.Lignes[0].Id,
                    Quantite = 1,
                    EtatArticle = EtatArticleRetour.Revendable
                }
            ],
            LignesNouvelles =
            [
                new LigneVenteDemande { VarianteProduitId = tailleL.Id, Quantite = 1, PrixUnitaire = 3000m }
            ],
            Paiements = [new PaiementDemande { ModePaiement = ModePaiement.Especes, Montant = 3000m }]
        });

        // La taille M est revenue en rayon, la taille L en est sortie.
        Assert.Equal(10, await StockAsync(tailleM.Id));
        Assert.Equal(5, await StockAsync(tailleL.Id));

        Assert.Equal(3000m, echange.Retour.TotalRembourse);
        Assert.Equal(3000m, echange.NouvelleVente.Total);

        // Les deux articles valent le même prix : rien à payer en plus.
        Assert.Equal(0m, echange.SoldeAPayer);
    }

    [Fact]
    public async Task Un_echange_vers_un_article_plus_cher_laisse_un_complement_a_payer()
    {
        var (_, articleVendu) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, "Pull simple", quantiteInitiale: 10, prixVente: 2000m);

        var (_, articleEchange) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, "Pull premium", quantiteInitiale: 10, prixVente: 3500m);

        var vente = await ConstructeurDonnees.VendreAsync(Ventes, articleVendu.Id, 1, 2000m);

        var echange = await Retours.EnregistrerEchangeAsync(new DemandeEchange
        {
            VenteId = vente.Id,
            LignesRetournees =
            [
                new LigneRetourDemande { LigneVenteId = vente.Lignes[0].Id, Quantite = 1 }
            ],
            LignesNouvelles =
            [
                new LigneVenteDemande { VarianteProduitId = articleEchange.Id, Quantite = 1, PrixUnitaire = 3500m }
            ],
            Paiements = [new PaiementDemande { ModePaiement = ModePaiement.Especes, Montant = 3500m }]
        });

        Assert.Equal(1500m, echange.SoldeAPayer);
        Assert.Equal(10, await StockAsync(articleVendu.Id));
        Assert.Equal(9, await StockAsync(articleEchange.Id));
    }

    [Fact]
    public async Task Un_echange_impossible_n_enregistre_ni_retour_ni_vente()
    {
        var (_, articleVendu) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, "Pull simple", quantiteInitiale: 10, prixVente: 2000m);

        // L'article de remplacement est en rupture.
        var (_, articleEnRupture) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, "Pull rare", quantiteInitiale: 0, prixVente: 2000m);

        var vente = await ConstructeurDonnees.VendreAsync(Ventes, articleVendu.Id, 1, 2000m);
        Assert.Equal(9, await StockAsync(articleVendu.Id));

        await Assert.ThrowsAsync<StockInsuffisantException>(() =>
            Retours.EnregistrerEchangeAsync(new DemandeEchange
            {
                VenteId = vente.Id,
                LignesRetournees = [new LigneRetourDemande { LigneVenteId = vente.Lignes[0].Id, Quantite = 1 }],
                LignesNouvelles =
                [
                    new LigneVenteDemande { VarianteProduitId = articleEnRupture.Id, Quantite = 1, PrixUnitaire = 2000m }
                ],
                Paiements = []
            }));

        // L'article rapporté n'a pas été réintégré : l'échange complet a été
        // annulé, le client repart avec son article d'origine.
        Assert.Equal(9, await StockAsync(articleVendu.Id));
        Assert.Equal(0, await StockAsync(articleEnRupture.Id));

        var retours = await Retours.RechercherAsync();
        Assert.Empty(retours);

        // Seule la vente initiale subsiste.
        var ventes = await Ventes.RechercherAsync();
        Assert.Single(ventes);
    }
}

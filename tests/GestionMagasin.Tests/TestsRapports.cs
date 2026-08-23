using GestionMagasin.Application.Common;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Enums;
using GestionMagasin.Tests.Socle;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Section 38 — Rapports : créer des ventes et des achats de test, puis
/// vérifier que les statistiques correspondent exactement aux données.
/// Chaque montant attendu est calculé à la main dans le test.
/// </summary>
public class TestsRapports : BaseDeTest
{
    private IServiceProduits Produits => Service<IServiceProduits>();
    private IServiceVentes Ventes => Service<IServiceVentes>();
    private IServiceAchats Achats => Service<IServiceAchats>();
    private IServiceRetours Retours => Service<IServiceRetours>();
    private IServiceFournisseurs Fournisseurs => Service<IServiceFournisseurs>();
    private IServiceRapports Rapports => Service<IServiceRapports>();

    private static Periode Aujourdhui => CalculateurPeriode.Aujourdhui(DateTime.Now);

    [Fact]
    public async Task Le_chiffre_d_affaires_correspond_a_la_somme_des_ventes()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 50, prixAchat: 1000m, prixVente: 2500m);

        await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 2, 2500m); // 5 000
        await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 3, 2500m); // 7 500
        await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 1, 2500m); // 2 500

        var synthese = await Rapports.ObtenirSyntheseAsync(Aujourdhui);

        Assert.Equal(15000m, synthese.ChiffreAffaires);
        Assert.Equal(15000m, synthese.ChiffreAffairesBrut);
        Assert.Equal(3, synthese.NombreVentes);
        Assert.Equal(6, synthese.ArticlesVendus);

        // Marge : 6 articles × (2 500 − 1 000) = 9 000 DA
        Assert.Equal(9000m, synthese.BeneficeEstime);

        // Ticket moyen : 15 000 / 3 = 5 000 DA
        Assert.Equal(5000m, synthese.TicketMoyen);
    }

    [Fact]
    public async Task Une_vente_annulee_est_exclue_des_statistiques()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 50, prixAchat: 1000m, prixVente: 2000m);

        await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 2, 2000m);
        var aAnnuler = await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 5, 2000m);

        var avant = await Rapports.ObtenirSyntheseAsync(Aujourdhui);
        Assert.Equal(14000m, avant.ChiffreAffaires);

        await Ventes.AnnulerVenteAsync(aAnnuler.Id, "Erreur de caisse");

        var apres = await Rapports.ObtenirSyntheseAsync(Aujourdhui);

        // Seule la première vente subsiste : 2 × 2 000 = 4 000 DA
        Assert.Equal(4000m, apres.ChiffreAffaires);
        Assert.Equal(1, apres.NombreVentes);
        Assert.Equal(2, apres.ArticlesVendus);
        Assert.Equal(2000m, apres.BeneficeEstime);
    }

    [Fact]
    public async Task Un_retour_diminue_le_chiffre_d_affaires_et_la_marge()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 50, prixAchat: 1000m, prixVente: 2500m);

        var vente = await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 4, 2500m);

        await Retours.EnregistrerRetourAsync(new DemandeRetour
        {
            VenteId = vente.Id,
            Lignes =
            [
                new LigneRetourDemande
                {
                    LigneVenteId = vente.Lignes[0].Id,
                    Quantite = 1,
                    EtatArticle = EtatArticleRetour.Revendable
                }
            ]
        });

        var synthese = await Rapports.ObtenirSyntheseAsync(Aujourdhui);

        // 10 000 vendus − 2 500 remboursés = 7 500 DA
        Assert.Equal(10000m, synthese.ChiffreAffairesBrut);
        Assert.Equal(2500m, synthese.MontantRetourne);
        Assert.Equal(7500m, synthese.ChiffreAffaires);
        Assert.Equal(1, synthese.NombreRetours);

        // Quatre articles sortis, un rendu : trois restent vendus.
        Assert.Equal(3, synthese.ArticlesVendus);

        // L'article revendable est revenu en stock : son coût d'achat est
        // annulé. Marge = 3 articles × 1 500 = 4 500 DA
        Assert.Equal(4500m, synthese.BeneficeEstime);
    }

    /// <summary>
    /// Un article vendu puis repris le même jour ne rapporte rien. Il figurait
    /// pourtant en tête des meilleures ventes du tableau de bord, juste sous
    /// un chiffre d'affaires à zéro : les deux chiffres se contredisaient.
    /// </summary>
    [Fact]
    public async Task Un_article_entierement_rendu_quitte_les_meilleures_ventes()
    {
        var (_, garde) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 50, prixAchat: 400m, prixVente: 1000m);

        var (_, rendu) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 50, prixAchat: 600m, prixVente: 1400m);

        await ConstructeurDonnees.VendreAsync(Ventes, garde.Id, 1, 1000m);
        var vente = await ConstructeurDonnees.VendreAsync(Ventes, rendu.Id, 3, 1400m);

        var avant = await Rapports.ObtenirMeilleuresVentesAsync(Aujourdhui);

        Assert.Equal(rendu.Id, avant[0].VarianteProduitId);
        Assert.Equal(3, avant[0].QuantiteVendue);

        await Retours.EnregistrerRetourAsync(new DemandeRetour
        {
            VenteId = vente.Id,
            Lignes =
            [
                new LigneRetourDemande
                {
                    LigneVenteId = vente.Lignes[0].Id,
                    Quantite = 3,
                    EtatArticle = EtatArticleRetour.Revendable
                }
            ]
        });

        var apres = await Rapports.ObtenirMeilleuresVentesAsync(Aujourdhui);

        // L'article intégralement rendu disparaît ; l'autre prend sa place.
        Assert.DoesNotContain(apres, c => c.VarianteProduitId == rendu.Id);
        Assert.Single(apres);
        Assert.Equal(garde.Id, apres[0].VarianteProduitId);
        Assert.Equal(1, apres[0].QuantiteVendue);
    }

    /// <summary>
    /// Un retour partiel n'efface pas l'article : il en réduit la quantité,
    /// le chiffre d'affaires et la marge.
    /// </summary>
    [Fact]
    public async Task Un_retour_partiel_reduit_le_classement_sans_l_effacer()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 50, prixAchat: 1000m, prixVente: 2500m);

        var vente = await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 4, 2500m);

        await Retours.EnregistrerRetourAsync(new DemandeRetour
        {
            VenteId = vente.Id,
            Lignes =
            [
                new LigneRetourDemande
                {
                    LigneVenteId = vente.Lignes[0].Id,
                    Quantite = 1,
                    EtatArticle = EtatArticleRetour.Revendable
                }
            ]
        });

        var classement = await Rapports.ObtenirMeilleuresVentesAsync(Aujourdhui);

        var ligne = Assert.Single(classement);

        Assert.Equal(3, ligne.QuantiteVendue);
        Assert.Equal(7500m, ligne.ChiffreAffaires);

        // L'article revendable annule son coût d'achat : 3 × 1 500 = 4 500 DA
        Assert.Equal(4500m, ligne.Benefice);

        // Le classement et la carte du tableau de bord disent la même chose.
        var synthese = await Rapports.ObtenirSyntheseAsync(Aujourdhui);

        Assert.Equal(synthese.ChiffreAffaires, ligne.ChiffreAffaires);
        Assert.Equal(synthese.BeneficeEstime, ligne.Benefice);
        Assert.Equal(synthese.ArticlesVendus, ligne.QuantiteVendue);
    }

    [Fact]
    public async Task Un_retour_endommage_pese_sur_la_marge()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 50, prixAchat: 1000m, prixVente: 2500m);

        var vente = await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 4, 2500m);

        await Retours.EnregistrerRetourAsync(new DemandeRetour
        {
            VenteId = vente.Id,
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

        var synthese = await Rapports.ObtenirSyntheseAsync(Aujourdhui);

        Assert.Equal(7500m, synthese.ChiffreAffaires);

        // L'article abîmé est remboursé sans revenir en stock : son coût
        // d'achat reste à la charge du magasin.
        // 10 000 − 4 000 (coût) − 2 500 (remboursement) = 3 500 DA
        Assert.Equal(3500m, synthese.BeneficeEstime);
    }

    [Fact]
    public async Task Le_classement_des_meilleures_ventes_est_exact()
    {
        var (_, populaire) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, "Article populaire", quantiteInitiale: 50, prixAchat: 500m, prixVente: 1000m);

        var (_, moyen) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, "Article moyen", quantiteInitiale: 50, prixAchat: 500m, prixVente: 1000m);

        var (_, rare) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, "Article rare", quantiteInitiale: 50, prixAchat: 500m, prixVente: 1000m);

        await ConstructeurDonnees.VendreAsync(Ventes, populaire.Id, 10, 1000m);
        await ConstructeurDonnees.VendreAsync(Ventes, moyen.Id, 5, 1000m);
        await ConstructeurDonnees.VendreAsync(Ventes, rare.Id, 1, 1000m);

        var classement = await Rapports.ObtenirMeilleuresVentesAsync(Aujourdhui);

        Assert.Equal(3, classement.Count);

        Assert.Equal(populaire.Id, classement[0].VarianteProduitId);
        Assert.Equal(10, classement[0].QuantiteVendue);
        Assert.Equal(10000m, classement[0].ChiffreAffaires);
        Assert.Equal(5000m, classement[0].Benefice);

        Assert.Equal(moyen.Id, classement[1].VarianteProduitId);
        Assert.Equal(5, classement[1].QuantiteVendue);

        Assert.Equal(rare.Id, classement[2].VarianteProduitId);
        Assert.Equal(1, classement[2].QuantiteVendue);
    }

    [Fact]
    public async Task Les_articles_jamais_vendus_apparaissent_dans_le_classement_inverse()
    {
        var (_, vendu) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, "Article vendu", quantiteInitiale: 20, prixVente: 1000m);

        var (_, invendu) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, "Article dormant", quantiteInitiale: 30, prixVente: 1000m);

        await ConstructeurDonnees.VendreAsync(Ventes, vendu.Id, 5, 1000m);

        var classement = await Rapports.ObtenirMoinsBonnesVentesAsync(Aujourdhui);

        // L'article jamais vendu, qui immobilise du stock, arrive en tête.
        Assert.Equal(invendu.Id, classement[0].VarianteProduitId);
        Assert.Equal(0, classement[0].QuantiteVendue);
        Assert.Equal(30, classement[0].StockActuel);
    }

    [Fact]
    public async Task Les_ventes_par_employe_sont_correctement_attribuees()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 50, prixVente: 1000m);

        // Deux ventes par le caissier.
        await ConnecterAsync(NomsRolesTest.Caissier);
        await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 2, 1000m);
        await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 3, 1000m);

        // Une vente par le responsable.
        await ConnecterAsync(NomsRolesTest.Responsable);
        await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 4, 1000m);

        await ConnecterAsync(NomsRolesTest.Administrateur);

        var parEmploye = await Rapports.ObtenirVentesParEmployeAsync(Aujourdhui);

        var caissier = parEmploye.First(e => e.Role == NomsRolesTest.Caissier);
        Assert.Equal(2, caissier.NombreVentes);
        Assert.Equal(5, caissier.ArticlesVendus);
        Assert.Equal(5000m, caissier.ChiffreAffaires);
        Assert.Equal(2500m, caissier.TicketMoyen);

        var responsable = parEmploye.First(e => e.Role == NomsRolesTest.Responsable);
        Assert.Equal(1, responsable.NombreVentes);
        Assert.Equal(4000m, responsable.ChiffreAffaires);

        // Le total par employé doit égaler le chiffre d'affaires global.
        var synthese = await Rapports.ObtenirSyntheseAsync(Aujourdhui);
        Assert.Equal(synthese.ChiffreAffairesBrut, parEmploye.Sum(e => e.ChiffreAffaires));
    }

    [Fact]
    public async Task La_repartition_par_mode_de_paiement_est_exacte()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 50, prixVente: 1000m);

        // 6 000 en espèces
        await Ventes.EnregistrerVenteAsync(new DemandeVente
        {
            Lignes = [new LigneVenteDemande { VarianteProduitId = variante.Id, Quantite = 6, PrixUnitaire = 1000m }],
            Paiements = [new PaiementDemande { ModePaiement = ModePaiement.Especes, Montant = 6000m }]
        });

        // 4 000 en carte bancaire
        await Ventes.EnregistrerVenteAsync(new DemandeVente
        {
            Lignes = [new LigneVenteDemande { VarianteProduitId = variante.Id, Quantite = 4, PrixUnitaire = 1000m }],
            Paiements = [new PaiementDemande { ModePaiement = ModePaiement.CarteBancaire, Montant = 4000m }]
        });

        var repartition = await Rapports.ObtenirVentesParModePaiementAsync(Aujourdhui);

        var especes = repartition.First(r => r.ModePaiement == "Espèces");
        Assert.Equal(6000m, especes.Montant);
        Assert.Equal(60m, especes.Pourcentage);

        var carte = repartition.First(r => r.ModePaiement == "Carte bancaire");
        Assert.Equal(4000m, carte.Montant);
        Assert.Equal(40m, carte.Pourcentage);

        Assert.Equal(100m, repartition.Sum(r => r.Pourcentage));
    }

    [Fact]
    public async Task La_synthese_des_achats_correspond_aux_commandes()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 0);
        var fournisseur = await ConstructeurDonnees.CreerFournisseurAsync(Fournisseurs);

        await Achats.CreerAchatAsync(new DemandeAchat
        {
            FournisseurId = fournisseur.Id,
            MontantPaye = 5000m,
            ReceptionImmediate = true,
            Lignes = [new LigneAchatDemande { VarianteProduitId = variante.Id, Quantite = 20, PrixUnitaire = 500m }]
        });

        await Achats.CreerAchatAsync(new DemandeAchat
        {
            FournisseurId = fournisseur.Id,
            Lignes = [new LigneAchatDemande { VarianteProduitId = variante.Id, Quantite = 10, PrixUnitaire = 500m }]
        });

        var synthese = await Rapports.ObtenirSyntheseAchatsAsync(Aujourdhui);

        Assert.Equal(2, synthese.NombreCommandes);
        Assert.Equal(30, synthese.ArticlesCommandes);
        Assert.Equal(20, synthese.ArticlesRecus);
        Assert.Equal(15000m, synthese.TotalCommandes);
        Assert.Equal(5000m, synthese.TotalPaye);
        Assert.Equal(10000m, synthese.TotalRestantDu);
    }

    [Fact]
    public async Task La_synthese_des_retours_distingue_les_articles_endommages()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 50, prixVente: 1000m);

        var vente = await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 10, 1000m);

        await Retours.EnregistrerRetourAsync(new DemandeRetour
        {
            VenteId = vente.Id,
            Lignes =
            [
                new LigneRetourDemande
                {
                    LigneVenteId = vente.Lignes[0].Id,
                    Quantite = 3,
                    EtatArticle = EtatArticleRetour.Revendable
                },
                new LigneRetourDemande
                {
                    LigneVenteId = vente.Lignes[0].Id,
                    Quantite = 2,
                    EtatArticle = EtatArticleRetour.Endommage
                }
            ]
        });

        var synthese = await Rapports.ObtenirSyntheseRetoursAsync(Aujourdhui);

        Assert.Equal(1, synthese.NombreRetours);
        Assert.Equal(5, synthese.ArticlesRepris);
        Assert.Equal(3, synthese.ArticlesRemisEnStock);
        Assert.Equal(2, synthese.ArticlesEndommages);
        Assert.Equal(5000m, synthese.TotalRembourse);

        // 5 articles repris sur 10 vendus = 50 %
        Assert.Equal(50m, synthese.TauxRetour);
    }

    [Fact]
    public async Task La_valorisation_du_stock_est_exacte()
    {
        await ConstructeurDonnees.CreerArticleAsync(
            Produits, "Article A", prixAchat: 1000m, prixVente: 2500m, quantiteInitiale: 4);

        await ConstructeurDonnees.CreerArticleAsync(
            Produits, "Article B", prixAchat: 600m, prixVente: 1500m, quantiteInitiale: 10);

        var synthese = await Rapports.ObtenirSyntheseStockAsync();

        Assert.Equal(14, synthese.QuantiteTotale);

        // 4 × 1 000 + 10 × 600 = 10 000 DA
        Assert.Equal(10000m, synthese.ValeurAuPrixAchat);

        // 4 × 2 500 + 10 × 1 500 = 25 000 DA
        Assert.Equal(25000m, synthese.ValeurAuPrixVente);

        Assert.Equal(15000m, synthese.MargePotentielle);
        Assert.Equal(2, synthese.NombreReferences);
    }

    [Fact]
    public async Task Une_vente_hors_periode_n_est_pas_comptee()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 50, prixVente: 1000m);

        await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 5, 1000m);

        // Période portant sur la journée d'hier : aucune vente ne doit y figurer.
        var hier = CalculateurPeriode.Aujourdhui(DateTime.Now.AddDays(-1));

        var synthese = await Rapports.ObtenirSyntheseAsync(hier);

        Assert.Equal(0m, synthese.ChiffreAffaires);
        Assert.Equal(0, synthese.NombreVentes);
    }

    [Fact]
    public async Task Le_tableau_de_bord_reprend_les_memes_chiffres_que_les_rapports()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 50, prixAchat: 800m, prixVente: 2000m, seuilMinimum: 45);

        await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 6, 2000m);

        var tableau = await Rapports.ObtenirTableauBordAsync();
        var synthese = await Rapports.ObtenirSyntheseAsync(Aujourdhui);

        Assert.Equal(synthese.ChiffreAffaires, tableau.Jour.ChiffreAffaires);
        Assert.Equal(synthese.NombreVentes, tableau.Jour.NombreVentes);
        Assert.Equal(synthese.BeneficeEstime, tableau.Jour.BeneficeEstime);

        // La courbe des trente derniers jours couvre bien trente points.
        Assert.Equal(30, tableau.EvolutionVentes.Count);
        Assert.Equal(12, tableau.EvolutionMensuelle.Count);

        // Le total de la courbe correspond au chiffre d'affaires brut du mois.
        var totalCourbe = tableau.EvolutionVentes.Sum(p => p.Valeur);
        Assert.Equal(12000m, totalCourbe);

        // L'article est passé sous son seuil d'alerte : il est signalé.
        Assert.Contains(tableau.StockFaible, l => l.VarianteProduitId == variante.Id);
        Assert.Single(tableau.DernieresVentes);
    }

    /// <summary>Noms de rôles utilisés par ces tests.</summary>
    private static class NomsRolesTest
    {
        public const string Administrateur = Domain.Securite.NomsRoles.Administrateur;
        public const string Responsable = Domain.Securite.NomsRoles.Responsable;
        public const string Caissier = Domain.Securite.NomsRoles.Caissier;
    }
}

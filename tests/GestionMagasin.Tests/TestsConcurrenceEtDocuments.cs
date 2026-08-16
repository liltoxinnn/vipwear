using GestionMagasin.Application.Common;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Enums;
using GestionMagasin.Domain.Exceptions;
using GestionMagasin.Tests.Socle;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Vérifie deux points que seule une vraie base de données peut démontrer :
/// la tenue du stock lorsque deux caisses vendent le même article au même
/// instant, et la production effective des documents imprimables.
/// </summary>
public class TestsConcurrenceEtDocuments : BaseDeTest
{
    private IServiceProduits Produits => Service<IServiceProduits>();
    private IServiceVentes Ventes => Service<IServiceVentes>();
    private IServiceAchats Achats => Service<IServiceAchats>();
    private IServiceRetours Retours => Service<IServiceRetours>();
    private IServiceFournisseurs Fournisseurs => Service<IServiceFournisseurs>();
    private IServiceStock Stock => Service<IServiceStock>();
    private IServiceRapports Rapports => Service<IServiceRapports>();
    private IServiceDocumentsPdf Pdf => Service<IServiceDocumentsPdf>();
    private IServiceExportExcel Excel => Service<IServiceExportExcel>();

    [Fact]
    public async Task Deux_caisses_ne_peuvent_pas_vendre_le_meme_dernier_article()
    {
        // Un seul exemplaire en stock, deux caisses le vendent en même temps.
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 1, prixVente: 2000m);

        var premiereCaisse = VendreDansUnePorteeAsync(variante.Id);
        var secondeCaisse = VendreDansUnePorteeAsync(variante.Id);

        var resultats = await Task.WhenAll(premiereCaisse, secondeCaisse);

        // Exactement une vente aboutit, l'autre est refusée pour stock
        // insuffisant : le verrou posé sur la ligne d'inventaire a fait son
        // travail.
        Assert.Equal(1, resultats.Count(r => r.Reussie));
        Assert.Equal(1, resultats.Count(r => !r.Reussie));

        Assert.Equal(0, await StockAsync(variante.Id));

        var ventes = await Ventes.RechercherAsync();
        Assert.Single(ventes);
    }

    [Fact]
    public async Task Cinq_ventes_simultanees_ne_font_jamais_passer_le_stock_sous_zero()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 3, prixVente: 1000m);

        var tentatives = Enumerable.Range(0, 5)
            .Select(_ => VendreDansUnePorteeAsync(variante.Id))
            .ToArray();

        var resultats = await Task.WhenAll(tentatives);

        Assert.Equal(3, resultats.Count(r => r.Reussie));
        Assert.Equal(0, await StockAsync(variante.Id));

        // Le stock ne peut jamais devenir négatif, quelle que soit la
        // simultanéité des opérations.
        var quantite = await Stock.ObtenirQuantiteAsync(variante.Id);
        Assert.True(quantite >= 0);
    }

    [Fact]
    public async Task Le_ticket_de_caisse_est_genere()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 10, prixVente: 2500m);

        var vente = await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 2, 2500m);

        var ticket = await Pdf.GenererTicketAsync(vente);

        Assert.NotEmpty(ticket);
        AssertEstUnPdf(ticket);
    }

    [Fact]
    public async Task La_facture_est_generee()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 10, prixVente: 2500m);

        var vente = await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 3, 2500m);

        var facture = await Pdf.GenererFactureAsync(vente);

        Assert.NotEmpty(facture);
        AssertEstUnPdf(facture);
    }

    [Fact]
    public async Task Le_bon_de_commande_et_le_bon_de_retour_sont_generes()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 10, prixVente: 2000m);

        var fournisseur = await ConstructeurDonnees.CreerFournisseurAsync(Fournisseurs);

        var achat = await Achats.CreerAchatAsync(new DemandeAchat
        {
            FournisseurId = fournisseur.Id,
            Lignes = [new LigneAchatDemande { VarianteProduitId = variante.Id, Quantite = 10, PrixUnitaire = 900m }]
        });

        var bonCommande = await Pdf.GenererBonCommandeAsync(achat);
        AssertEstUnPdf(bonCommande);

        var vente = await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 2, 2000m);

        var retour = await Retours.EnregistrerRetourAsync(new DemandeRetour
        {
            VenteId = vente.Id,
            Motif = "Taille inadaptée",
            Lignes = [new LigneRetourDemande { LigneVenteId = vente.Lignes[0].Id, Quantite = 1 }]
        });

        var bonRetour = await Pdf.GenererBonRetourAsync(retour);
        AssertEstUnPdf(bonRetour);
    }

    [Fact]
    public async Task Les_rapports_pdf_sont_generes()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 20, prixVente: 2000m);

        await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 5, 2000m);

        var periode = CalculateurPeriode.Aujourdhui(DateTime.Now);

        var rapportActivite = await Pdf.GenererRapportActiviteAsync(
            periode,
            await Rapports.ObtenirSyntheseAsync(periode),
            await Rapports.ObtenirMeilleuresVentesAsync(periode),
            await Rapports.ObtenirVentesParEmployeAsync(periode),
            await Rapports.ObtenirVentesParModePaiementAsync(periode));

        AssertEstUnPdf(rapportActivite);

        var rapportStock = await Pdf.GenererRapportStockAsync(
            await Stock.ListerStockAsync(),
            await Rapports.ObtenirSyntheseStockAsync());

        AssertEstUnPdf(rapportStock);
    }

    [Fact]
    public async Task Les_exports_excel_sont_generes()
    {
        var (produit, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 20, prixVente: 2000m);

        await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 4, 2000m);

        var periode = CalculateurPeriode.Aujourdhui(DateTime.Now);

        AssertEstUnClasseurExcel(await Excel.ExporterStockAsync(await Stock.ListerStockAsync()));

        AssertEstUnClasseurExcel(await Excel.ExporterProduitsAsync(
            [(await Produits.ObtenirProduitAsync(produit.Id))!]));

        AssertEstUnClasseurExcel(await Excel.ExporterVentesAsync(
            await Ventes.RechercherAsync(), periode));

        AssertEstUnClasseurExcel(await Excel.ExporterMouvementsStockAsync(
            await Stock.ObtenirHistoriqueAsync()));

        AssertEstUnClasseurExcel(await Excel.ExporterRapportActiviteAsync(
            periode,
            await Rapports.ObtenirSyntheseAsync(periode),
            await Rapports.ObtenirMeilleuresVentesAsync(periode),
            await Rapports.ObtenirMoinsBonnesVentesAsync(periode),
            await Rapports.ObtenirVentesParEmployeAsync(periode),
            await Rapports.ObtenirVentesParModePaiementAsync(periode)));
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Exécute une vente dans sa propre portée de services, comme le ferait
    /// un second poste de caisse avec sa propre connexion.
    /// </summary>
    private async Task<(bool Reussie, string? Erreur)> VendreDansUnePorteeAsync(int varianteId)
    {
        try
        {
            using var portee = CreerPortee();
            var ventes = portee.ServiceProvider.GetRequiredService<IServiceVentes>();

            await ventes.EnregistrerVenteAsync(new DemandeVente
            {
                Lignes = [new LigneVenteDemande { VarianteProduitId = varianteId, Quantite = 1, PrixUnitaire = 1000m }],
                Paiements = [new PaiementDemande { ModePaiement = ModePaiement.Especes, Montant = 1000m }]
            }).ConfigureAwait(false);

            return (true, null);
        }
        catch (StockInsuffisantException erreur)
        {
            return (false, erreur.Message);
        }
    }

    /// <summary>Un PDF valide commence toujours par la signature « %PDF- ».</summary>
    private static void AssertEstUnPdf(byte[] contenu)
    {
        Assert.True(contenu.Length > 500, "Le document PDF généré est anormalement petit.");

        var signature = System.Text.Encoding.ASCII.GetString(contenu, 0, 5);
        Assert.Equal("%PDF-", signature);
    }

    /// <summary>Un classeur .xlsx est une archive ZIP : elle commence par « PK ».</summary>
    private static void AssertEstUnClasseurExcel(byte[] contenu)
    {
        Assert.True(contenu.Length > 500, "Le classeur Excel généré est anormalement petit.");

        Assert.Equal(0x50, contenu[0]);
        Assert.Equal(0x4B, contenu[1]);
    }
}

using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;

namespace GestionMagasin.Tests.Socle;

/// <summary>
/// Raccourcis de création des données nécessaires aux tests. Tout passe par
/// les services réels du logiciel : les tests exercent donc exactement le
/// même chemin que l'interface, contrôles de permission compris.
/// </summary>
public static class ConstructeurDonnees
{
    /// <summary>Crée un produit accompagné d'une déclinaison approvisionnée.</summary>
    public static async Task<(ProduitDto Produit, VarianteDto Variante)> CreerArticleAsync(
        IServiceProduits produits,
        string nom = "T-shirt Nike",
        string? reference = null,
        decimal prixAchat = 1200m,
        decimal prixVente = 2500m,
        int quantiteInitiale = 10,
        int seuilMinimum = 3,
        string? codeBarres = null,
        string categorie = "T-shirts et polos",
        string taille = "M")
    {
        var suffixe = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var familles = await produits.ListerCategoriesAsync().ConfigureAwait(false);
        var famille = familles.First(c => c.Nom == categorie);

        var produit = await produits.CreerProduitAsync(new DemandeProduit
        {
            Reference = reference ?? $"REF-{suffixe}",
            Sku = $"SKU-{suffixe}",
            Nom = nom,
            CategorieId = famille.Id,
            PrixAchat = prixAchat,
            PrixVente = prixVente
        }).ConfigureAwait(false);

        // Les tailles proposées sont celles du système de la famille : une
        // chaussure ne se décline pas en XXL.
        var tailles = await produits
            .ListerTaillesAsync(systemeTailleId: famille.SystemeTailleId)
            .ConfigureAwait(false);

        var couleurs = await produits.ListerCouleursAsync().ConfigureAwait(false);

        var variante = await produits.AjouterVarianteAsync(produit.Id, new DemandeVariante
        {
            TailleId = tailles.First(t => t.Nom == taille).Id,
            CouleurId = couleurs.First(c => c.Nom == "Noir").Id,
            CodeBarres = codeBarres ?? $"600{suffixe}",
            SeuilMinimum = seuilMinimum,
            QuantiteInitiale = quantiteInitiale
        }).ConfigureAwait(false);

        var complet = await produits.ObtenirProduitAsync(produit.Id).ConfigureAwait(false);

        return (complet!, variante);
    }

    /// <summary>Ajoute une déclinaison supplémentaire à un produit existant.</summary>
    public static async Task<VarianteDto> AjouterDeclinaisonAsync(
        IServiceProduits produits,
        int produitId,
        string taille,
        string couleur,
        int quantiteInitiale = 5,
        int seuilMinimum = 2)
    {
        var fiche = await produits.ObtenirProduitAsync(produitId).ConfigureAwait(false);

        var tailles = await produits
            .ListerTaillesAsync(systemeTailleId: fiche!.SystemeTailleId)
            .ConfigureAwait(false);

        var couleurs = await produits.ListerCouleursAsync().ConfigureAwait(false);

        return await produits.AjouterVarianteAsync(produitId, new DemandeVariante
        {
            TailleId = tailles.First(t => t.Nom == taille).Id,
            CouleurId = couleurs.First(c => c.Nom == couleur).Id,
            CodeBarres = $"600{Guid.NewGuid():N}"[..13],
            SeuilMinimum = seuilMinimum,
            QuantiteInitiale = quantiteInitiale
        }).ConfigureAwait(false);
    }

    /// <summary>Encaisse une vente réglée en espèces.</summary>
    public static Task<VenteDto> VendreAsync(
        IServiceVentes ventes,
        int varianteId,
        int quantite,
        decimal prixUnitaire,
        int? clientId = null)
    {
        var total = prixUnitaire * quantite;

        return ventes.EnregistrerVenteAsync(new DemandeVente
        {
            ClientId = clientId,
            Lignes =
            [
                new LigneVenteDemande
                {
                    VarianteProduitId = varianteId,
                    Quantite = quantite,
                    PrixUnitaire = prixUnitaire
                }
            ],
            Paiements =
            [
                new PaiementDemande
                {
                    ModePaiement = Domain.Enums.ModePaiement.Especes,
                    Montant = total
                }
            ]
        });
    }

    /// <summary>Crée un fournisseur destiné aux tests d'achat.</summary>
    public static Task<FournisseurDto> CreerFournisseurAsync(
        IServiceFournisseurs fournisseurs,
        string nom = "Fournisseur Textile Alger") =>
        fournisseurs.CreerAsync(new DemandeFournisseur
        {
            Nom = nom,
            Entreprise = "SARL Textile",
            Telephone = "0550123456"
        });

    /// <summary>Crée un client destiné aux tests de vente et de retour.</summary>
    public static Task<ClientDto> CreerClientAsync(
        IServiceClients clients,
        string nom = "Benali",
        string prenom = "Karim") =>
        clients.CreerAsync(new DemandeClient
        {
            Nom = nom,
            Prenom = prenom,
            Telephone = $"055{Random.Shared.Next(1000000, 9999999)}"
        });
}

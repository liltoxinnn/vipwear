using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Exceptions;
using GestionMagasin.Tests.Socle;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Section 38 — Produit : créer, modifier, désactiver, rechercher.
/// Vérifie également qu'aucun doublon de déclinaison ne peut être créé.
/// </summary>
public class TestsProduits : BaseDeTest
{
    private IServiceProduits Produits => Service<IServiceProduits>();

    [Fact]
    public async Task Creer_produit_enregistre_la_fiche_et_ses_declinaisons()
    {
        var (produit, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, "T-shirt Nike", prixAchat: 1200m, prixVente: 2500m, quantiteInitiale: 12);

        Assert.Equal("T-shirt Nike", produit.Nom);
        Assert.True(produit.Actif);
        Assert.Single(produit.Variantes);
        Assert.Equal(12, produit.StockTotal);

        // Le prix de la déclinaison suit celui du produit tant qu'il n'est pas
        // redéfini spécifiquement.
        Assert.Equal(2500m, variante.PrixVente);
        Assert.Equal(1200m, variante.PrixAchat);
        Assert.Equal(1300m, produit.Marge);
    }

    [Fact]
    public async Task Modifier_produit_met_a_jour_la_fiche()
    {
        var (produit, _) = await ConstructeurDonnees.CreerArticleAsync(Produits);

        var modifie = await Produits.ModifierProduitAsync(produit.Id, new DemandeProduit
        {
            Reference = produit.Reference,
            Sku = produit.Sku,
            Nom = "T-shirt Nike Sport",
            CategorieId = produit.CategorieId,
            Description = "Coton biologique",
            Collection = "Été 2026",
            Saison = "Été",
            PrixAchat = 1300m,
            PrixVente = 2900m
        });

        Assert.Equal("T-shirt Nike Sport", modifie.Nom);
        Assert.Equal("Été 2026", modifie.Collection);
        Assert.Equal(2900m, modifie.PrixVente);
        Assert.NotNull(modifie.DateModification);
    }

    [Fact]
    public async Task Desactiver_produit_le_retire_des_listes_sans_effacer_la_fiche()
    {
        var (produit, _) = await ConstructeurDonnees.CreerArticleAsync(Produits);

        await Produits.DefinirEtatProduitAsync(produit.Id, actif: false);

        var listeVente = await Produits.RechercherProduitsAsync();
        Assert.DoesNotContain(listeVente, p => p.Id == produit.Id);

        // La fiche existe toujours : elle est simplement inactive.
        var listeComplete = await Produits.RechercherProduitsAsync(inclureInactifs: true);
        Assert.Contains(listeComplete, p => p.Id == produit.Id && !p.Actif);

        var fiche = await Produits.ObtenirProduitAsync(produit.Id);
        Assert.NotNull(fiche);
        Assert.False(fiche.Actif);
    }

    [Fact]
    public async Task Rechercher_produit_trouve_par_nom_reference_et_code_barres()
    {
        var (produit, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, "Chemise Zara", codeBarres: "6130000000123");

        var parNom = await Produits.RechercherProduitsAsync("chemise");
        Assert.Contains(parNom, p => p.Id == produit.Id);

        var parReference = await Produits.RechercherProduitsAsync(produit.Reference);
        Assert.Contains(parReference, p => p.Id == produit.Id);

        var parCodeBarres = await Produits.RechercherProduitsAsync("6130000000123");
        Assert.Contains(parCodeBarres, p => p.Id == produit.Id);

        // La recherche en caisse retrouve directement la déclinaison scannée.
        var scannee = await Produits.ObtenirVarianteParCodeBarresAsync("6130000000123");
        Assert.NotNull(scannee);
        Assert.Equal(variante.Id, scannee.Id);
    }

    [Fact]
    public async Task Creer_deux_fois_la_meme_combinaison_est_refuse()
    {
        var (produit, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits);

        var erreur = await Assert.ThrowsAsync<ValidationMetierException>(() =>
            Produits.AjouterVarianteAsync(produit.Id, new DemandeVariante
            {
                TailleId = variante.TailleId,
                CouleurId = variante.CouleurId
            }));

        Assert.Contains("existe déjà", erreur.Message);
    }

    [Fact]
    public async Task Un_code_barres_ne_peut_pas_etre_attribue_deux_fois()
    {
        var (produit, _) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, codeBarres: "6130000000999");

        var tailles = await Produits.ListerTaillesAsync();
        var couleurs = await Produits.ListerCouleursAsync();

        var erreur = await Assert.ThrowsAsync<ValidationMetierException>(() =>
            Produits.AjouterVarianteAsync(produit.Id, new DemandeVariante
            {
                TailleId = tailles.First(t => t.Nom == "L").Id,
                CouleurId = couleurs.First(c => c.Nom == "Noir").Id,
                CodeBarres = "6130000000999"
            }));

        Assert.Contains("déjà attribué", erreur.Message);
    }

    [Fact]
    public async Task Generer_les_declinaisons_ne_cree_jamais_de_doublon()
    {
        var (produit, _) = await ConstructeurDonnees.CreerArticleAsync(Produits);

        var tailles = await Produits.ListerTaillesAsync();
        var couleurs = await Produits.ListerCouleursAsync();

        var idsTailles = tailles.Where(t => t.Nom is "S" or "M" or "L").Select(t => t.Id).ToList();
        var idsCouleurs = couleurs.Where(c => c.Nom is "Noir" or "Blanc").Select(c => c.Id).ToList();

        // Six combinaisons possibles, dont une (Noir/M) existe déjà.
        var creees = await Produits.GenererVariantesAsync(produit.Id, idsTailles, idsCouleurs);
        Assert.Equal(5, creees);

        // Relancer la génération ne recrée rien.
        var secondPassage = await Produits.GenererVariantesAsync(produit.Id, idsTailles, idsCouleurs);
        Assert.Equal(0, secondPassage);

        var fiche = await Produits.ObtenirProduitAsync(produit.Id);
        Assert.Equal(6, fiche!.NombreVariantes);
    }

    [Fact]
    public async Task Une_reference_deja_utilisee_est_refusee()
    {
        var (produit, _) = await ConstructeurDonnees.CreerArticleAsync(Produits);

        var erreur = await Assert.ThrowsAsync<ValidationMetierException>(() =>
            Produits.CreerProduitAsync(new DemandeProduit
            {
                Reference = produit.Reference,
                Sku = "SKU-DIFFERENT",
                Nom = "Autre produit",
                CategorieId = produit.CategorieId,
                PrixAchat = 100m,
                PrixVente = 200m
            }));

        Assert.Contains("déjà utilisée", erreur.Message);
    }

    [Fact]
    public async Task Un_prix_negatif_est_refuse()
    {
        var familles = await Produits.ListerCategoriesAsync();

        var erreur = await Assert.ThrowsAsync<ValidationMetierException>(() =>
            Produits.CreerProduitAsync(new DemandeProduit
            {
                Reference = "REF-NEG",
                Sku = "SKU-NEG",
                Nom = "Produit invalide",
                CategorieId = familles[0].Id,
                PrixAchat = -10m,
                PrixVente = 200m
            }));

        Assert.Contains("négatif", erreur.Message);
    }

    /// <summary>
    /// Un article sans famille ne saurait pas quelles tailles proposer, et
    /// n'aurait pas de rayon.
    /// </summary>
    [Fact]
    public async Task Un_produit_sans_famille_est_refuse()
    {
        var erreur = await Assert.ThrowsAsync<ValidationMetierException>(() =>
            Produits.CreerProduitAsync(new DemandeProduit
            {
                Reference = "REF-SANS-FAMILLE",
                Sku = "SKU-SANS-FAMILLE",
                Nom = "Produit sans famille",
                PrixAchat = 100m,
                PrixVente = 200m
            }));

        Assert.Contains("famille", erreur.Message);
    }

    /// <summary>
    /// Chaque famille impose son système de tailles : chemises en lettres,
    /// pantalons en tailles, chaussures en pointures.
    /// </summary>
    [Fact]
    public async Task Chaque_famille_propose_les_tailles_de_son_systeme()
    {
        var familles = await Produits.ListerCategoriesAsync();

        var chemises = familles.First(c => c.Nom == "Chemises");
        var chaussures = familles.First(c => c.Nom == "Chaussures");
        var pantalons = familles.First(c => c.Nom == "Pantalons");

        var lettres = await Produits.ListerTaillesAsync(systemeTailleId: chemises.SystemeTailleId);
        var pointures = await Produits.ListerTaillesAsync(systemeTailleId: chaussures.SystemeTailleId);
        var tailles = await Produits.ListerTaillesAsync(systemeTailleId: pantalons.SystemeTailleId);

        Assert.Contains(lettres, t => t.Nom == "M");
        Assert.DoesNotContain(lettres, t => t.Nom == "42");

        Assert.Contains(pointures, t => t.Nom == "42");
        Assert.DoesNotContain(pointures, t => t.Nom == "M");

        Assert.Contains(tailles, t => t.Nom == "38");
        Assert.DoesNotContain(tailles, t => t.Nom == "XXL");

        // « 42 » existe des deux côtés sans être la même chose.
        Assert.NotEqual(
            pointures.First(t => t.Nom == "42").Id,
            tailles.First(t => t.Nom == "42").Id);
    }

    /// <summary>
    /// Une chaussure en XXL est invendable, et le stock la garderait. Le
    /// contrôle vit dans le service, pas seulement dans l'écran : un import
    /// ou un enchaînement d'écrans le contournerait autrement.
    /// </summary>
    [Fact]
    public async Task Une_taille_etrangere_a_la_famille_est_refusee()
    {
        var (chaussure, _) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, nom: "Mocassin cuir", categorie: "Chaussures", taille: "42");

        var lettres = await Produits.ListerTaillesAsync();
        var xxl = lettres.First(t => t.Nom == "XXL");
        var couleurs = await Produits.ListerCouleursAsync();

        var erreur = await Assert.ThrowsAsync<ValidationMetierException>(() =>
            Produits.AjouterVarianteAsync(chaussure.Id, new DemandeVariante
            {
                TailleId = xxl.Id,
                CouleurId = couleurs[0].Id,
                QuantiteInitiale = 1
            }));

        Assert.Contains("système", erreur.Message);
    }
}

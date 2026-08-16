using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Enums;
using GestionMagasin.Domain.Exceptions;
using GestionMagasin.Domain.Securite;
using GestionMagasin.Tests.Socle;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Section 38 — Permissions : vérifie que chaque rôle dispose exactement des
/// autorisations prévues, et que les contrôles sont bien appliqués par les
/// services eux-mêmes et non par la seule interface.
/// </summary>
public class TestsPermissions : BaseDeTest
{
    private IServiceProduits Produits => Service<IServiceProduits>();
    private IServiceVentes Ventes => Service<IServiceVentes>();
    private IServiceStock Stock => Service<IServiceStock>();
    private IServiceUtilisateurs Utilisateurs => Service<IServiceUtilisateurs>();
    private IServiceRapports Rapports => Service<IServiceRapports>();

    [Fact]
    public async Task L_administrateur_dispose_de_toutes_les_permissions()
    {
        await ConnecterAsync(NomsRoles.Administrateur);

        foreach (var code in CodesPermissions.Tous)
        {
            Assert.True(Session.Possede(code), $"L'administrateur devrait posséder « {code} ».");
        }
    }

    [Fact]
    public async Task Le_responsable_gere_le_magasin_mais_pas_les_comptes()
    {
        await ConnecterAsync(NomsRoles.Responsable);

        // Ce qu'il peut faire
        Assert.True(Session.Possede(CodesPermissions.ModifierProduits));
        Assert.True(Session.Possede(CodesPermissions.ModifierPrix));
        Assert.True(Session.Possede(CodesPermissions.ModifierStock));
        Assert.True(Session.Possede(CodesPermissions.GererAchats));
        Assert.True(Session.Possede(CodesPermissions.EffectuerVente));
        Assert.True(Session.Possede(CodesPermissions.AccorderRemise));
        Assert.True(Session.Possede(CodesPermissions.VoirRapports));

        // Ce qui lui est refusé
        Assert.False(Session.Possede(CodesPermissions.GererUtilisateurs));
        Assert.False(Session.Possede(CodesPermissions.GererParametres));
        Assert.False(Session.Possede(CodesPermissions.VoirJournalAudit));
    }

    [Fact]
    public async Task Le_caissier_encaisse_sans_pouvoir_modifier_le_catalogue()
    {
        await ConnecterAsync(NomsRoles.Caissier);

        // Ce qu'il peut faire
        Assert.True(Session.Possede(CodesPermissions.EffectuerVente));
        Assert.True(Session.Possede(CodesPermissions.EffectuerRetour));
        Assert.True(Session.Possede(CodesPermissions.VoirProduits));
        Assert.True(Session.Possede(CodesPermissions.VoirStock));
        Assert.True(Session.Possede(CodesPermissions.GererClients));

        // Ce qui lui est refusé
        Assert.False(Session.Possede(CodesPermissions.ModifierProduits));
        Assert.False(Session.Possede(CodesPermissions.ModifierPrix));
        Assert.False(Session.Possede(CodesPermissions.ModifierStock));
        Assert.False(Session.Possede(CodesPermissions.AccorderRemise));
        Assert.False(Session.Possede(CodesPermissions.AnnulerVente));
        Assert.False(Session.Possede(CodesPermissions.VoirRapports));
        Assert.False(Session.Possede(CodesPermissions.GererUtilisateurs));
        Assert.False(Session.Possede(CodesPermissions.GererAchats));
    }

    [Fact]
    public async Task Un_caissier_ne_peut_pas_creer_de_produit()
    {
        await ConnecterAsync(NomsRoles.Caissier);

        await Assert.ThrowsAsync<AccesRefuseException>(() =>
            Produits.CreerProduitAsync(new DemandeProduit
            {
                Reference = "REF-INTERDIT",
                Sku = "SKU-INTERDIT",
                Nom = "Produit interdit",
                PrixAchat = 100m,
                PrixVente = 200m
            }));
    }

    [Fact]
    public async Task Un_caissier_ne_peut_pas_modifier_le_stock_manuellement()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 10);

        await ConnecterAsync(NomsRoles.Caissier);

        await Assert.ThrowsAsync<AccesRefuseException>(() =>
            Stock.AjusterStockAsync(variante.Id, 50, "Tentative non autorisée"));

        await Assert.ThrowsAsync<AccesRefuseException>(() =>
            Stock.EnregistrerEntreeManuelleAsync(
                variante.Id, 10, TypeMouvementStock.Correction, "Tentative non autorisée"));

        // Le stock est resté intact.
        Assert.Equal(10, await StockAsync(variante.Id));
    }

    [Fact]
    public async Task Un_caissier_peut_vendre_mais_pas_accorder_de_remise()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(
            Produits, quantiteInitiale: 10, prixVente: 2000m);

        await ConnecterAsync(NomsRoles.Caissier);

        // La vente simple fonctionne.
        var vente = await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 1, 2000m);
        Assert.Equal(2000m, vente.Total);
        Assert.Equal(9, await StockAsync(variante.Id));

        // La remise est refusée.
        await Assert.ThrowsAsync<AccesRefuseException>(() =>
            Ventes.EnregistrerVenteAsync(new DemandeVente
            {
                RemiseGlobale = 500m,
                Lignes = [new LigneVenteDemande { VarianteProduitId = variante.Id, Quantite = 1, PrixUnitaire = 2000m }],
                Paiements = [new PaiementDemande { ModePaiement = ModePaiement.Especes, Montant = 1500m }]
            }));

        // La vente refusée n'a pas entamé le stock.
        Assert.Equal(9, await StockAsync(variante.Id));
    }

    [Fact]
    public async Task Un_caissier_ne_peut_pas_annuler_une_vente()
    {
        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 10);
        var vente = await ConstructeurDonnees.VendreAsync(Ventes, variante.Id, 2, 2500m);

        await ConnecterAsync(NomsRoles.Caissier);

        await Assert.ThrowsAsync<AccesRefuseException>(() =>
            Ventes.AnnulerVenteAsync(vente.Id, "Tentative non autorisée"));

        Assert.Equal(8, await StockAsync(variante.Id));
    }

    [Fact]
    public async Task Un_caissier_ne_peut_pas_consulter_les_rapports()
    {
        await ConnecterAsync(NomsRoles.Caissier);

        await Assert.ThrowsAsync<AccesRefuseException>(() =>
            Rapports.ObtenirTableauBordAsync());

        await Assert.ThrowsAsync<AccesRefuseException>(() =>
            Rapports.ObtenirSyntheseStockAsync());
    }

    [Fact]
    public async Task Un_responsable_ne_peut_pas_gerer_les_comptes()
    {
        await ConnecterAsync(NomsRoles.Responsable);

        await Assert.ThrowsAsync<AccesRefuseException>(() => Utilisateurs.ListerAsync());

        await Assert.ThrowsAsync<AccesRefuseException>(() =>
            Utilisateurs.CreerAsync(new DemandeUtilisateur
            {
                Nom = "Test",
                Prenom = "Test",
                NomUtilisateur = "nouveau",
                MotDePasse = "MotDePasse2026",
                RoleId = 1
            }));
    }

    [Fact]
    public async Task Un_responsable_peut_gerer_le_catalogue_et_le_stock()
    {
        await ConnecterAsync(NomsRoles.Responsable);

        var (_, variante) = await ConstructeurDonnees.CreerArticleAsync(Produits, quantiteInitiale: 10);

        var nouvelle = await Stock.AjusterStockAsync(variante.Id, 15, "Inventaire du responsable");
        Assert.Equal(15, nouvelle);

        var rapport = await Rapports.ObtenirSyntheseStockAsync();
        Assert.True(rapport.QuantiteTotale >= 15);
    }

    [Fact]
    public async Task Un_compte_ne_peut_pas_se_desactiver_lui_meme()
    {
        var moi = Session.UtilisateurIdObligatoire;

        var erreur = await Assert.ThrowsAsync<ValidationMetierException>(() =>
            Utilisateurs.DefinirEtatAsync(moi, actif: false));

        Assert.Contains("votre propre compte", erreur.Message);
    }

    [Fact]
    public async Task Le_dernier_administrateur_ne_peut_pas_etre_desactive()
    {
        // Un profil de supervision est créé : il gère les comptes sans être
        // lui-même administrateur. C'est le seul cas où un utilisateur peut
        // tenter de désactiver le dernier administrateur du magasin.
        var superviseur = await Utilisateurs.EnregistrerRoleAsync(
            null, "Superviseur", "Gestion des comptes uniquement");

        await Utilisateurs.DefinirPermissionsAsync(
            superviseur.Id,
            [CodesPermissions.GererUtilisateurs]);

        await Utilisateurs.CreerAsync(new DemandeUtilisateur
        {
            Nom = "Superviseur",
            Prenom = "Compte",
            NomUtilisateur = "superviseur",
            MotDePasse = "Superviseur2026",
            RoleId = superviseur.Id
        });

        // Tous les administrateurs sont désactivés sauf le compte connecté.
        var moi = Session.UtilisateurIdObligatoire;

        var autresAdministrateurs = (await Utilisateurs.ListerAsync())
            .Where(u => u.Role == NomsRoles.Administrateur && u.Actif && u.Id != moi)
            .ToList();

        foreach (var compte in autresAdministrateurs)
        {
            await Utilisateurs.DefinirEtatAsync(compte.Id, actif: false);
        }

        var dernierAdministrateur = (await Utilisateurs.ListerAsync())
            .Single(u => u.Role == NomsRoles.Administrateur && u.Actif);

        Assert.Equal(moi, dernierAdministrateur.Id);

        // Le superviseur tente de désactiver le dernier administrateur.
        await ConnecterAsync("Superviseur", "superviseur");

        var erreur = await Assert.ThrowsAsync<ValidationMetierException>(() =>
            Utilisateurs.DefinirEtatAsync(dernierAdministrateur.Id, actif: false));

        Assert.Contains("dernier administrateur", erreur.Message);

        // Le compte est toujours actif : le magasin reste administrable.
        var apres = await Utilisateurs.ObtenirAsync(dernierAdministrateur.Id);
        Assert.True(apres!.Actif);
    }

    [Fact]
    public async Task Le_role_administrateur_conserve_la_gestion_des_comptes()
    {
        var roles = await Utilisateurs.ListerRolesAsync();
        var administrateur = roles.First(r => r.Nom == NomsRoles.Administrateur);

        var erreur = await Assert.ThrowsAsync<ValidationMetierException>(() =>
            Utilisateurs.DefinirPermissionsAsync(
                administrateur.Id,
                [CodesPermissions.VoirProduits]));

        Assert.Contains("Gérer les utilisateurs", erreur.Message);
    }

    [Fact]
    public async Task Les_permissions_d_un_role_peuvent_etre_ajustees()
    {
        var roles = await Utilisateurs.ListerRolesAsync();
        var caissier = roles.First(r => r.Nom == NomsRoles.Caissier);

        // Le magasin décide d'autoriser ses caissiers à accorder des remises.
        var codes = caissier.Permissions.Select(p => p.Code).ToList();
        codes.Add(CodesPermissions.AccorderRemise);

        var modifie = await Utilisateurs.DefinirPermissionsAsync(caissier.Id, codes);

        Assert.Contains(modifie.Permissions, p => p.Code == CodesPermissions.AccorderRemise);

        // La nouvelle autorisation s'applique dès la prochaine connexion.
        await ConnecterAsync(NomsRoles.Caissier);
        Assert.True(Session.Possede(CodesPermissions.AccorderRemise));
    }

    [Fact]
    public async Task Un_role_systeme_ne_peut_pas_etre_supprime()
    {
        var roles = await Utilisateurs.ListerRolesAsync();
        var caissier = roles.First(r => r.Nom == NomsRoles.Caissier);

        var erreur = await Assert.ThrowsAsync<ValidationMetierException>(() =>
            Utilisateurs.SupprimerRoleAsync(caissier.Id));

        Assert.Contains("système", erreur.Message);
    }
}

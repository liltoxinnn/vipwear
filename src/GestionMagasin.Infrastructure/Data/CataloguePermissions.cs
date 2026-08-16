using GestionMagasin.Domain.Securite;

namespace GestionMagasin.Infrastructure.Data;

/// <summary>
/// Description en français de chaque permission et composition des trois
/// rôles livrés d'origine. Ce catalogue est la source unique consultée lors
/// de l'amorçage de la base.
/// </summary>
internal static class CataloguePermissions
{
    internal sealed record DefinitionPermission(string Code, string Libelle, string Description, string Categorie);

    internal sealed record DefinitionRole(string Nom, string Description, IReadOnlyList<string> Permissions);

    public static IReadOnlyList<DefinitionPermission> Definitions { get; } =
    [
        new(CodesPermissions.VoirProduits, "Consulter les produits",
            "Accéder au catalogue et aux fiches produits.", "Produits"),
        new(CodesPermissions.ModifierProduits, "Gérer les produits",
            "Créer et modifier les produits, les variantes, les tailles et les couleurs.", "Produits"),
        new(CodesPermissions.SupprimerProduits, "Désactiver les produits",
            "Retirer un produit de la vente sans effacer son historique.", "Produits"),
        new(CodesPermissions.ModifierPrix, "Modifier les prix",
            "Changer les prix d'achat et de vente.", "Produits"),

        new(CodesPermissions.VoirStock, "Consulter le stock",
            "Voir les quantités disponibles et l'historique des mouvements.", "Stock"),
        new(CodesPermissions.ModifierStock, "Gérer le stock",
            "Ajouter, retirer et ajuster les quantités en stock.", "Stock"),

        new(CodesPermissions.EffectuerVente, "Encaisser une vente",
            "Utiliser la caisse et enregistrer les ventes.", "Ventes"),
        new(CodesPermissions.AccorderRemise, "Accorder une remise",
            "Appliquer une remise sur une ligne ou sur un ticket.", "Ventes"),
        new(CodesPermissions.AnnulerVente, "Annuler une vente",
            "Annuler une vente déjà enregistrée et réintégrer le stock.", "Ventes"),
        new(CodesPermissions.EffectuerRetour, "Enregistrer un retour",
            "Reprendre des articles vendus et rembourser le client.", "Ventes"),

        new(CodesPermissions.VoirAchats, "Consulter les achats",
            "Voir les commandes fournisseurs.", "Achats"),
        new(CodesPermissions.GererAchats, "Gérer les achats",
            "Créer les commandes fournisseurs et réceptionner la marchandise.", "Achats"),
        new(CodesPermissions.GererFournisseurs, "Gérer les fournisseurs",
            "Créer et modifier les fiches fournisseurs.", "Achats"),

        new(CodesPermissions.VoirClients, "Consulter les clients",
            "Voir les fiches clients et leur historique d'achats.", "Clients"),
        new(CodesPermissions.GererClients, "Gérer les clients",
            "Créer et modifier les fiches clients.", "Clients"),

        new(CodesPermissions.VoirRapports, "Consulter les rapports",
            "Accéder aux statistiques, au chiffre d'affaires et aux bénéfices.", "Pilotage"),
        new(CodesPermissions.GererUtilisateurs, "Gérer les utilisateurs",
            "Créer les comptes, attribuer les rôles et réinitialiser les mots de passe.", "Pilotage"),
        new(CodesPermissions.GererParametres, "Gérer les paramètres",
            "Modifier les informations du magasin et la configuration du logiciel.", "Pilotage"),
        new(CodesPermissions.VoirJournalAudit, "Consulter le journal d'audit",
            "Voir l'historique des actions réalisées dans le logiciel.", "Pilotage")
    ];

    public static IReadOnlyList<DefinitionRole> RolesParDefaut { get; } =
    [
        // L'administrateur dispose de la totalité des autorisations.
        new(NomsRoles.Administrateur,
            "Accès complet au logiciel, y compris les utilisateurs et les paramètres.",
            CodesPermissions.Tous),

        // Le responsable pilote le magasin au quotidien mais ne gère ni les
        // comptes ni la configuration du logiciel.
        new(NomsRoles.Responsable,
            "Gestion quotidienne du magasin : catalogue, stock, achats, ventes et rapports.",
            [
                CodesPermissions.VoirProduits,
                CodesPermissions.ModifierProduits,
                CodesPermissions.SupprimerProduits,
                CodesPermissions.ModifierPrix,
                CodesPermissions.VoirStock,
                CodesPermissions.ModifierStock,
                CodesPermissions.EffectuerVente,
                CodesPermissions.AccorderRemise,
                CodesPermissions.AnnulerVente,
                CodesPermissions.EffectuerRetour,
                CodesPermissions.VoirAchats,
                CodesPermissions.GererAchats,
                CodesPermissions.GererFournisseurs,
                CodesPermissions.VoirClients,
                CodesPermissions.GererClients,
                CodesPermissions.VoirRapports
            ]),

        // Le caissier travaille en caisse : il vend, encaisse et consulte,
        // sans pouvoir modifier les prix ni le stock.
        new(NomsRoles.Caissier,
            "Utilisation de la caisse : encaissement, retours et consultation du catalogue.",
            [
                CodesPermissions.VoirProduits,
                CodesPermissions.VoirStock,
                CodesPermissions.EffectuerVente,
                CodesPermissions.EffectuerRetour,
                CodesPermissions.VoirClients,
                CodesPermissions.GererClients
            ])
    ];
}

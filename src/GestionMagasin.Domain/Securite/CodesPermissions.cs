namespace GestionMagasin.Domain.Securite;

/// <summary>
/// Codes techniques des permissions gérées par le logiciel.
/// Ils servent de clé stable entre le code et la table des permissions :
/// le libellé affiché peut évoluer, le code ne change jamais.
/// </summary>
public static class CodesPermissions
{
    // --- Produits ---
    public const string VoirProduits = "VoirProduits";
    public const string ModifierProduits = "ModifierProduits";
    public const string SupprimerProduits = "SupprimerProduits";
    public const string ModifierPrix = "ModifierPrix";

    // --- Stock ---
    public const string VoirStock = "VoirStock";
    public const string ModifierStock = "ModifierStock";

    // --- Caisse et ventes ---
    public const string EffectuerVente = "EffectuerVente";
    public const string AccorderRemise = "AccorderRemise";
    public const string AnnulerVente = "AnnulerVente";
    public const string EffectuerRetour = "EffectuerRetour";

    // --- Achats et fournisseurs ---
    public const string VoirAchats = "VoirAchats";
    public const string GererAchats = "GererAchats";
    public const string GererFournisseurs = "GererFournisseurs";

    // --- Clients ---
    public const string VoirClients = "VoirClients";
    public const string GererClients = "GererClients";

    // --- Pilotage ---
    public const string VoirRapports = "VoirRapports";
    public const string GererUtilisateurs = "GererUtilisateurs";
    public const string GererParametres = "GererParametres";
    public const string VoirJournalAudit = "VoirJournalAudit";

    /// <summary>Ensemble complet des codes, utilisé pour l'amorçage de la base.</summary>
    public static IReadOnlyList<string> Tous { get; } = new[]
    {
        VoirProduits, ModifierProduits, SupprimerProduits, ModifierPrix,
        VoirStock, ModifierStock,
        EffectuerVente, AccorderRemise, AnnulerVente, EffectuerRetour,
        VoirAchats, GererAchats, GererFournisseurs,
        VoirClients, GererClients,
        VoirRapports, GererUtilisateurs, GererParametres, VoirJournalAudit
    };
}

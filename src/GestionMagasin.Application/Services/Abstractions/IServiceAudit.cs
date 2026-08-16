namespace GestionMagasin.Application.Services.Abstractions;

/// <summary>
/// Enregistre les actions importantes réalisées dans le logiciel.
/// L'écriture rejoint la transaction en cours lorsqu'il y en a une : la trace
/// est donc annulée en même temps que l'opération si celle-ci échoue.
/// </summary>
public interface IServiceAudit
{
    /// <summary>Ajoute une entrée au journal, sans enregistrer immédiatement.</summary>
    Task TracerAsync(
        string action,
        string typeEntite,
        int? entiteId,
        string description,
        CancellationToken jeton = default);

    /// <summary>Ajoute une entrée au journal et l'enregistre aussitôt.</summary>
    Task TracerEtEnregistrerAsync(
        string action,
        string typeEntite,
        int? entiteId,
        string description,
        CancellationToken jeton = default);
}

/// <summary>Libellés normalisés des actions tracées.</summary>
public static class ActionsAudit
{
    public const string Creation = "Création";
    public const string Modification = "Modification";
    public const string Suppression = "Suppression";
    public const string Desactivation = "Désactivation";
    public const string Activation = "Activation";
    public const string Connexion = "Connexion";
    public const string Deconnexion = "Déconnexion";
    public const string EchecConnexion = "Échec de connexion";
    public const string Vente = "Vente";
    public const string AnnulationVente = "Annulation de vente";
    public const string Achat = "Achat";
    public const string Reception = "Réception";
    public const string Retour = "Retour";
    public const string MouvementStock = "Mouvement de stock";
    public const string ChangementPrix = "Changement de prix";
    public const string ChangementMotDePasse = "Changement de mot de passe";
}

using GestionMagasin.Application.DTOs;
using GestionMagasin.Domain.Enums;

namespace GestionMagasin.Application.Services.Abstractions;

/// <summary>
/// Point d'entrée unique de toute modification de stock. Aucun autre
/// composant du logiciel n'écrit dans la table des inventaires : c'est ce qui
/// garantit qu'une quantité et son historique restent toujours cohérents.
///
/// Chaque opération verrouille la ligne concernée, vérifie la règle métier,
/// met à jour la quantité et crée le mouvement correspondant, le tout dans
/// une même transaction.
/// </summary>
public interface IServiceStock
{
    /// <summary>
    /// Augmente le stock d'une déclinaison. Utilisé par les réceptions
    /// d'achat et les corrections d'entrée.
    /// </summary>
    /// <returns>La nouvelle quantité disponible.</returns>
    Task<int> AjouterStockAsync(
        int varianteProduitId,
        int quantite,
        TypeMouvementStock typeMouvement,
        string? motif = null,
        string? referenceDocument = null,
        CancellationToken jeton = default);

    /// <summary>
    /// Diminue le stock d'une déclinaison après avoir vérifié la disponibilité.
    /// </summary>
    /// <exception cref="Domain.Exceptions.StockInsuffisantException">
    /// Levée lorsque la quantité demandée dépasse la quantité disponible.
    /// </exception>
    /// <returns>La nouvelle quantité disponible.</returns>
    Task<int> RetirerStockAsync(
        int varianteProduitId,
        int quantite,
        TypeMouvementStock typeMouvement,
        string? motif = null,
        string? referenceDocument = null,
        CancellationToken jeton = default);

    /// <summary>
    /// Réintègre au stock un article rapporté par un client et jugé revendable.
    /// </summary>
    /// <returns>La nouvelle quantité disponible.</returns>
    Task<int> RetournerStockAsync(
        int varianteProduitId,
        int quantite,
        string? motif = null,
        string? referenceDocument = null,
        CancellationToken jeton = default);

    /// <summary>
    /// Fixe la quantité d'une déclinaison à la valeur constatée lors d'un
    /// inventaire physique. L'écart est tracé comme un ajustement motivé.
    /// Requiert la permission « Gérer le stock ».
    /// </summary>
    /// <returns>La nouvelle quantité disponible.</returns>
    Task<int> AjusterStockAsync(
        int varianteProduitId,
        int quantiteConstatee,
        string motif,
        CancellationToken jeton = default);

    /// <summary>
    /// Entrée de stock saisie manuellement par un responsable.
    /// Requiert la permission « Gérer le stock ».
    /// </summary>
    Task<int> EnregistrerEntreeManuelleAsync(
        int varianteProduitId,
        int quantite,
        TypeMouvementStock typeMouvement,
        string motif,
        CancellationToken jeton = default);

    /// <summary>
    /// Sortie de stock saisie manuellement (perte, casse, retour fournisseur).
    /// Requiert la permission « Gérer le stock ».
    /// </summary>
    Task<int> EnregistrerSortieManuelleAsync(
        int varianteProduitId,
        int quantite,
        TypeMouvementStock typeMouvement,
        string motif,
        CancellationToken jeton = default);

    /// <summary>Indique si la quantité demandée est disponible.</summary>
    Task<bool> VerifierDisponibiliteAsync(
        int varianteProduitId,
        int quantite,
        CancellationToken jeton = default);

    /// <summary>
    /// Vérifie la disponibilité et interrompt l'opération si elle est insuffisante.
    /// </summary>
    Task ExigerDisponibiliteAsync(
        int varianteProduitId,
        int quantite,
        CancellationToken jeton = default);

    /// <summary>Quantité actuellement disponible pour une déclinaison.</summary>
    Task<int> ObtenirQuantiteAsync(int varianteProduitId, CancellationToken jeton = default);

    /// <summary>Historique des mouvements, du plus récent au plus ancien.</summary>
    Task<IReadOnlyList<MouvementStockDto>> ObtenirHistoriqueAsync(
        int? varianteProduitId = null,
        DateTime? debut = null,
        DateTime? fin = null,
        TypeMouvementStock? typeMouvement = null,
        int limite = 500,
        CancellationToken jeton = default);

    /// <summary>État du stock, avec filtres facultatifs.</summary>
    Task<IReadOnlyList<LigneStockDto>> ListerStockAsync(
        string? recherche = null,
        bool uniquementStockFaible = false,
        bool uniquementRuptures = false,
        bool inclureInactifs = false,
        CancellationToken jeton = default);

    /// <summary>Déclinaisons passées sous leur seuil d'alerte.</summary>
    Task<IReadOnlyList<LigneStockDto>> ListerStockFaibleAsync(int limite = 100, CancellationToken jeton = default);

    /// <summary>Déclinaisons dont la quantité est tombée à zéro.</summary>
    Task<IReadOnlyList<LigneStockDto>> ListerRupturesAsync(int limite = 100, CancellationToken jeton = default);

    /// <summary>Valeur totale du stock, calculée au prix d'achat.</summary>
    Task<decimal> CalculerValeurStockAsync(CancellationToken jeton = default);
}

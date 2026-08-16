using GestionMagasin.Domain.Entities;

namespace GestionMagasin.Domain.Interfaces;

/// <summary>
/// Accès aux quantités en stock. Ce dépôt est réservé au service de stock :
/// aucun autre composant ne doit écrire dans l'inventaire.
/// </summary>
public interface IDepotInventaire : IDepotGenerique<Inventaire>
{
    /// <summary>
    /// Charge la ligne d'inventaire d'une variante en la verrouillant jusqu'à
    /// la fin de la transaction. Deux caisses qui vendent le même article au
    /// même instant sont ainsi traitées l'une après l'autre, ce qui rend le
    /// stock négatif impossible.
    /// </summary>
    Task<Inventaire?> ObtenirPourMiseAJourAsync(int varianteProduitId, CancellationToken jeton = default);

    /// <summary>Charge la ligne d'inventaire d'une variante sans la verrouiller.</summary>
    Task<Inventaire?> ObtenirParVarianteAsync(int varianteProduitId, CancellationToken jeton = default);

    /// <summary>Quantité disponible d'une variante, 0 si aucune ligne n'existe.</summary>
    Task<int> ObtenirQuantiteAsync(int varianteProduitId, CancellationToken jeton = default);

    /// <summary>Quantités disponibles pour un lot de variantes, indexées par variante.</summary>
    Task<IReadOnlyDictionary<int, int>> ObtenirQuantitesAsync(IEnumerable<int> varianteProduitIds, CancellationToken jeton = default);
}

using GestionMagasin.Domain.Common;

namespace GestionMagasin.Domain.Interfaces;

/// <summary>
/// Point d'entrée unique de la persistance. Regroupe les dépôts et pilote les
/// transactions : les opérations critiques (vente, achat, retour) s'exécutent
/// entièrement ou pas du tout.
/// </summary>
public interface IUniteDeTravail
{
    IDepotVariantes Variantes { get; }

    IDepotInventaire Inventaire { get; }

    IDepotUtilisateurs Utilisateurs { get; }

    /// <summary>Dépôt générique pour les entités ne nécessitant pas de requêtes spécifiques.</summary>
    IDepotGenerique<T> Depot<T>() where T : EntiteBase;

    /// <summary>Écrit en base les modifications suivies et retourne le nombre de lignes touchées.</summary>
    Task<int> EnregistrerAsync(CancellationToken jeton = default);

    /// <summary>
    /// Ouvre une transaction. Si une transaction est déjà ouverte, retourne un
    /// jeton imbriqué inoffensif : seule la transaction la plus externe valide
    /// réellement, ce qui permet de composer les services entre eux.
    /// </summary>
    Task<ITransactionMetier> DemarrerTransactionAsync(CancellationToken jeton = default);

    /// <summary>Indique qu'une transaction est actuellement ouverte.</summary>
    bool TransactionEnCours { get; }

    /// <summary>
    /// Vide le suivi des entités chargées. Utilisé entre deux opérations
    /// longues pour éviter de conserver un cache obsolète en mémoire.
    /// </summary>
    void ViderSuivi();
}

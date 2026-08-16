using GestionMagasin.Domain.Common;

namespace GestionMagasin.Domain.Interfaces;

/// <summary>
/// Accès aux données commun à toutes les entités. Les requêtes complexes sont
/// composées par les services à partir de <see cref="Requete"/>.
/// </summary>
public interface IDepotGenerique<T> where T : EntiteBase
{
    /// <summary>Charge une entité par son identifiant, ou null si elle n'existe pas.</summary>
    Task<T?> ObtenirParIdAsync(int id, CancellationToken jeton = default);

    /// <summary>
    /// Requête en lecture seule, sans suivi des modifications : à utiliser pour
    /// tout affichage. Plus rapide et sans effet de bord.
    /// </summary>
    IQueryable<T> Requete();

    /// <summary>
    /// Requête avec suivi des modifications : à utiliser lorsque les entités
    /// chargées vont être modifiées puis enregistrées.
    /// </summary>
    IQueryable<T> RequeteSuivie();

    Task AjouterAsync(T entite, CancellationToken jeton = default);

    Task AjouterPlusieursAsync(IEnumerable<T> entites, CancellationToken jeton = default);

    void Modifier(T entite);

    /// <summary>
    /// Suppression physique. Réservée aux données sans valeur historique :
    /// les produits, clients et fournisseurs sont désactivés, pas supprimés.
    /// </summary>
    void Supprimer(T entite);
}

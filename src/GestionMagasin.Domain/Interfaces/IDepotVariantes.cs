using GestionMagasin.Domain.Entities;

namespace GestionMagasin.Domain.Interfaces;

/// <summary>Accès aux déclinaisons de produits, notamment par code-barres en caisse.</summary>
public interface IDepotVariantes : IDepotGenerique<VarianteProduit>
{
    /// <summary>
    /// Recherche la variante correspondant à un code-barres scanné, avec son
    /// produit, sa taille, sa couleur et son inventaire déjà chargés.
    /// </summary>
    Task<VarianteProduit?> ObtenirParCodeBarresAsync(string codeBarres, CancellationToken jeton = default);

    /// <summary>
    /// Articles vendables en caisse : actifs, appartenant à un produit actif,
    /// et effectivement disponibles en rayon. Classés par produit, couleur
    /// puis taille, dans l'ordre où le caissier les cherche des yeux.
    /// </summary>
    Task<IReadOnlyList<VarianteProduit>> ListerVendablesAsync(int limite = 400, CancellationToken jeton = default);

    /// <summary>Recherche une variante par son code article.</summary>
    Task<VarianteProduit?> ObtenirParSkuAsync(string sku, CancellationToken jeton = default);

    /// <summary>Charge une variante avec toutes ses données d'affichage.</summary>
    Task<VarianteProduit?> ObtenirCompleteAsync(int id, CancellationToken jeton = default);

    /// <summary>
    /// Indique si la combinaison produit + taille + couleur existe déjà,
    /// afin de refuser la création d'un doublon avant d'atteindre la base.
    /// </summary>
    Task<bool> CombinaisonExisteAsync(int produitId, int tailleId, int couleurId, int? idAExclure = null, CancellationToken jeton = default);

    /// <summary>Indique si un code-barres est déjà attribué à une autre variante.</summary>
    Task<bool> CodeBarresExisteAsync(string codeBarres, int? idAExclure = null, CancellationToken jeton = default);

    /// <summary>Indique si un code article est déjà attribué à une autre variante.</summary>
    Task<bool> SkuExisteAsync(string sku, int? idAExclure = null, CancellationToken jeton = default);

    /// <summary>
    /// Recherche libre utilisée en caisse : porte sur le code-barres, le code
    /// article, la référence et le nom du produit.
    /// </summary>
    Task<IReadOnlyList<VarianteProduit>> RechercherAsync(string terme, int limite = 50, CancellationToken jeton = default);
}

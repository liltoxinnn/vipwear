using GestionMagasin.Domain.Common;

namespace GestionMagasin.Domain.Entities;

/// <summary>
/// Action élémentaire autorisée dans le logiciel (voir un produit, encaisser,
/// modifier un prix...). Le système est extensible : ajouter une ligne dans
/// cette table suffit à créer une nouvelle autorisation.
/// </summary>
public class Permission : EntiteBase
{
    /// <summary>Code technique stable, voir <see cref="Securite.CodesPermissions"/>.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Libellé affiché à l'écran, en français.</summary>
    public string Nom { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Regroupement utilisé pour l'affichage (Produits, Stock, Ventes...).</summary>
    public string Categorie { get; set; } = string.Empty;

    public ICollection<RolePermission> Roles { get; set; } = new List<RolePermission>();
}

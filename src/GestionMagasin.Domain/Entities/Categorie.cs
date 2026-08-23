using GestionMagasin.Domain.Common;

namespace GestionMagasin.Domain.Entities;

/// <summary>
/// Famille d'articles : chemises, pantalons, chaussures, accessoires.
///
/// La catégorie détermine le système de tailles proposé à la création des
/// déclinaisons. Elle sert aussi à lire le catalogue et les rapports par
/// rayon, ce qu'un magasin de vêtements fait naturellement.
/// </summary>
public class Categorie : EntiteBase
{
    public string Nom { get; set; } = string.Empty;

    /// <summary>Système de tailles employé par les articles de cette famille.</summary>
    public int SystemeTailleId { get; set; }

    public SystemeTaille SystemeTaille { get; set; } = null!;

    /// <summary>Position d'affichage dans les listes.</summary>
    public int Ordre { get; set; }

    public bool Actif { get; set; } = true;

    public ICollection<Produit> Produits { get; set; } = new List<Produit>();
}

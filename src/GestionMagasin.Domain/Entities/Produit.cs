using GestionMagasin.Domain.Common;

namespace GestionMagasin.Domain.Entities;

/// <summary>
/// Modèle d'article vendu par le magasin (« T-shirt Nike »).
/// Le produit ne porte volontairement AUCUNE quantité : le stock est tenu
/// au niveau de chaque <see cref="VarianteProduit"/> (couleur + taille),
/// seule granularité qui reflète la réalité du magasin.
/// Les prix définis ici servent de valeurs par défaut aux variantes.
/// </summary>
public class Produit : EntiteBase
{
    /// <summary>Référence interne lisible par le personnel.</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Code article unique du modèle.</summary>
    public string SKU { get; set; } = string.Empty;

    public string Nom { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int? MarqueId { get; set; }

    public Marque? Marque { get; set; }

    /// <summary>Collection commerciale (« Printemps 2026 », « Basique »...).</summary>
    public string? Collection { get; set; }

    /// <summary>Saison de commercialisation (« Été », « Hiver »...).</summary>
    public string? Saison { get; set; }

    /// <summary>Prix d'achat par défaut, en DZD.</summary>
    public decimal PrixAchat { get; set; }

    /// <summary>Prix de vente par défaut, en DZD.</summary>
    public decimal PrixVente { get; set; }

    /// <summary>Chemin relatif de la photo du produit dans le dossier de données.</summary>
    public string? Photo { get; set; }

    /// <summary>Un produit désactivé n'est plus proposé à la vente mais reste dans l'historique.</summary>
    public bool Actif { get; set; } = true;

    public DateTime DateCreation { get; set; }

    public DateTime? DateModification { get; set; }

    public ICollection<VarianteProduit> Variantes { get; set; } = new List<VarianteProduit>();
}

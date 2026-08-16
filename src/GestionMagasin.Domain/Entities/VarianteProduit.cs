using GestionMagasin.Domain.Common;

namespace GestionMagasin.Domain.Entities;

/// <summary>
/// Déclinaison concrète d'un produit : un produit + une taille + une couleur
/// (« T-shirt Nike / Noir / M »). C'est l'unité réellement scannée, vendue,
/// achetée et stockée. Une contrainte d'unicité en base interdit deux fois la
/// même combinaison Produit + Taille + Couleur.
/// </summary>
public class VarianteProduit : EntiteBase
{
    public int ProduitId { get; set; }

    public Produit Produit { get; set; } = null!;

    public int TailleId { get; set; }

    public Taille Taille { get; set; } = null!;

    public int CouleurId { get; set; }

    public Couleur Couleur { get; set; } = null!;

    /// <summary>Code article propre à la déclinaison, unique dans tout le magasin.</summary>
    public string SKU { get; set; } = string.Empty;

    /// <summary>Code-barres scanné en caisse. Unique lorsqu'il est renseigné.</summary>
    public string? CodeBarres { get; set; }

    /// <summary>
    /// Prix d'achat spécifique à la déclinaison. Laisser vide pour utiliser
    /// celui du produit, afin d'éviter de dupliquer une information identique.
    /// </summary>
    public decimal? PrixAchat { get; set; }

    /// <summary>Prix de vente spécifique à la déclinaison, sinon celui du produit.</summary>
    public decimal? PrixVente { get; set; }

    /// <summary>Quantité en dessous de laquelle la variante est signalée en stock faible.</summary>
    public int SeuilMinimum { get; set; }

    public bool Actif { get; set; } = true;

    public Inventaire? Inventaire { get; set; }

    public ICollection<MouvementStock> MouvementsStock { get; set; } = new List<MouvementStock>();

    /// <summary>Prix d'achat réellement appliqué (variante si défini, sinon produit).</summary>
    public decimal PrixAchatEffectif => PrixAchat ?? Produit?.PrixAchat ?? 0m;

    /// <summary>Prix de vente réellement appliqué (variante si défini, sinon produit).</summary>
    public decimal PrixVenteEffectif => PrixVente ?? Produit?.PrixVente ?? 0m;

    /// <summary>Libellé complet affiché dans les listes et sur les tickets.</summary>
    public string Designation =>
        $"{Produit?.Nom} — {Couleur?.Nom} / {Taille?.Nom}".Trim(' ', '—');
}

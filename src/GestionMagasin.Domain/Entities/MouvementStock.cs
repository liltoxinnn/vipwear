using GestionMagasin.Domain.Common;
using GestionMagasin.Domain.Enums;

namespace GestionMagasin.Domain.Entities;

/// <summary>
/// Trace immuable d'une variation de stock. Chaque passage par le service de
/// stock produit exactement une ligne, ce qui permet de reconstituer à tout
/// moment l'historique complet d'une déclinaison.
/// </summary>
public class MouvementStock : EntiteBase
{
    public int VarianteProduitId { get; set; }

    public VarianteProduit VarianteProduit { get; set; } = null!;

    public TypeMouvementStock TypeMouvement { get; set; }

    /// <summary>Quantité du mouvement, signée : positive en entrée, négative en sortie.</summary>
    public int Quantite { get; set; }

    public int AncienneQuantite { get; set; }

    public int NouvelleQuantite { get; set; }

    /// <summary>Explication saisie par l'utilisateur ou générée par le logiciel.</summary>
    public string? Motif { get; set; }

    /// <summary>Numéro du document à l'origine du mouvement (vente, achat, retour).</summary>
    public string? ReferenceDocument { get; set; }

    public int? UtilisateurId { get; set; }

    public Utilisateur? Utilisateur { get; set; }

    public DateTime DateCreation { get; set; }
}

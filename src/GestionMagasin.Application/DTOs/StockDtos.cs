using GestionMagasin.Domain.Enums;

namespace GestionMagasin.Application.DTOs;

/// <summary>Ligne de stock affichée dans l'écran « Stock » et le tableau de bord.</summary>
public record LigneStockDto
{
    public int VarianteProduitId { get; init; }

    public int ProduitId { get; init; }

    public string ProduitNom { get; init; } = string.Empty;

    public string Reference { get; init; } = string.Empty;

    public string Sku { get; init; } = string.Empty;

    public string? CodeBarres { get; init; }

    public string? Marque { get; init; }

    public string Couleur { get; init; } = string.Empty;

    public string? CodeCouleur { get; init; }

    public string Taille { get; init; } = string.Empty;

    public int OrdreTaille { get; init; }

    public int QuantiteDisponible { get; init; }

    public int SeuilMinimum { get; init; }

    public decimal PrixAchat { get; init; }

    public decimal PrixVente { get; init; }

    public bool Actif { get; init; }

    /// <summary>Valeur immobilisée par cette ligne, au prix d'achat.</summary>
    public decimal ValeurStock => QuantiteDisponible * PrixAchat;

    /// <summary>Vrai lorsque la quantité est tombée à zéro.</summary>
    public bool EnRupture => QuantiteDisponible <= 0;

    /// <summary>Vrai lorsque la quantité est passée sous le seuil d'alerte.</summary>
    public bool StockFaible => QuantiteDisponible > 0
                               && SeuilMinimum > 0
                               && QuantiteDisponible <= SeuilMinimum;

    /// <summary>Libellé complet de l'article, utilisé dans les listes et les documents.</summary>
    public string Designation => $"{ProduitNom} — {Couleur} / {Taille}";

    /// <summary>État lisible affiché dans la colonne « Situation ».</summary>
    public string LibelleEtat => EnRupture ? "Rupture" : StockFaible ? "Stock faible" : "Disponible";
}

/// <summary>Entrée d'historique affichée dans la fiche d'une déclinaison.</summary>
public record MouvementStockDto
{
    public int Id { get; init; }

    public int VarianteProduitId { get; init; }

    public string Designation { get; init; } = string.Empty;

    public string Sku { get; init; } = string.Empty;

    public TypeMouvementStock TypeMouvement { get; init; }

    public string LibelleTypeMouvement { get; init; } = string.Empty;

    /// <summary>Quantité signée : positive pour une entrée, négative pour une sortie.</summary>
    public int Quantite { get; init; }

    public int AncienneQuantite { get; init; }

    public int NouvelleQuantite { get; init; }

    public string? Motif { get; init; }

    public string? ReferenceDocument { get; init; }

    public string? Utilisateur { get; init; }

    public DateTime DateCreation { get; init; }

    /// <summary>Sens du mouvement, affiché sous forme de flèche dans l'interface.</summary>
    public bool EstEntree => Quantite > 0;
}

using GestionMagasin.Domain.Enums;

namespace GestionMagasin.Application.DTOs;

/// <summary>Article commandé auprès d'un fournisseur.</summary>
public record LigneAchatDemande
{
    public int VarianteProduitId { get; init; }

    public int Quantite { get; init; }

    /// <summary>
    /// Prix d'achat négocié. Laisser vide pour reprendre le prix d'achat
    /// actuellement enregistré sur la déclinaison.
    /// </summary>
    public decimal? PrixUnitaire { get; init; }
}

/// <summary>Commande fournisseur à enregistrer.</summary>
public record DemandeAchat
{
    public int FournisseurId { get; init; }

    public decimal Remise { get; init; }

    public decimal MontantPaye { get; init; }

    public string? Notes { get; init; }

    /// <summary>
    /// Réceptionner immédiatement la totalité de la commande. Pratique pour
    /// un achat comptant où la marchandise repart avec l'acheteur.
    /// </summary>
    public bool ReceptionImmediate { get; init; }

    public IReadOnlyList<LigneAchatDemande> Lignes { get; init; } = Array.Empty<LigneAchatDemande>();
}

/// <summary>Quantité effectivement reçue pour une ligne de commande.</summary>
public record LigneReception
{
    public int LigneAchatId { get; init; }

    public int QuantiteRecue { get; init; }
}

/// <summary>Ligne de détail d'une commande fournisseur.</summary>
public record LigneAchatDto
{
    public int Id { get; init; }

    public int VarianteProduitId { get; init; }

    public string Designation { get; init; } = string.Empty;

    public string Sku { get; init; } = string.Empty;

    public string Couleur { get; init; } = string.Empty;

    public string Taille { get; init; } = string.Empty;

    public int Quantite { get; init; }

    public int QuantiteRecue { get; init; }

    public decimal PrixUnitaire { get; init; }

    public decimal Total { get; init; }

    public int QuantiteRestante => Quantite - QuantiteRecue;
}

/// <summary>Commande fournisseur complète.</summary>
public record AchatDto
{
    public int Id { get; init; }

    public string NumeroAchat { get; init; } = string.Empty;

    public int FournisseurId { get; init; }

    public string FournisseurNom { get; init; } = string.Empty;

    public string? FournisseurTelephone { get; init; }

    public string UtilisateurNom { get; init; } = string.Empty;

    public DateTime DateAchat { get; init; }

    public DateTime? DateReception { get; init; }

    public decimal SousTotal { get; init; }

    public decimal Remise { get; init; }

    public decimal Total { get; init; }

    public decimal MontantPaye { get; init; }

    public decimal MontantRestant { get; init; }

    public StatutAchat Statut { get; init; }

    public string LibelleStatut { get; init; } = string.Empty;

    public string? Notes { get; init; }

    public IReadOnlyList<LigneAchatDto> Lignes { get; init; } = Array.Empty<LigneAchatDto>();

    public int NombreArticles => Lignes.Sum(l => l.Quantite);

    /// <summary>Vrai tant qu'il reste de la marchandise à réceptionner.</summary>
    public bool PeutEtreReceptionne => Statut is StatutAchat.EnAttente or StatutAchat.PartiellementRecu;
}

/// <summary>Ligne de la liste des achats.</summary>
public record ResumeAchatDto
{
    public int Id { get; init; }

    public string NumeroAchat { get; init; } = string.Empty;

    public DateTime DateAchat { get; init; }

    public string FournisseurNom { get; init; } = string.Empty;

    public int NombreArticles { get; init; }

    public decimal Total { get; init; }

    public decimal MontantPaye { get; init; }

    public decimal MontantRestant { get; init; }

    public StatutAchat Statut { get; init; }

    public string LibelleStatut { get; init; } = string.Empty;
}

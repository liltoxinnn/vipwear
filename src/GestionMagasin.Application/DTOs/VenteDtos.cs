using GestionMagasin.Domain.Enums;

namespace GestionMagasin.Application.DTOs;

/// <summary>Article ajouté au panier de la caisse.</summary>
public record LigneVenteDemande
{
    public int VarianteProduitId { get; init; }

    public int Quantite { get; init; }

    /// <summary>
    /// Prix appliqué. Laisser vide pour reprendre automatiquement le prix de
    /// vente en vigueur de la déclinaison.
    /// </summary>
    public decimal? PrixUnitaire { get; init; }

    /// <summary>Remise accordée sur la ligne, en DZD.</summary>
    public decimal Remise { get; init; }
}

/// <summary>Règlement présenté par le client.</summary>
public record PaiementDemande
{
    public ModePaiement ModePaiement { get; init; }

    public decimal Montant { get; init; }

    public string? Reference { get; init; }
}

/// <summary>Demande d'encaissement transmise par la caisse au service des ventes.</summary>
public record DemandeVente
{
    public int? ClientId { get; init; }

    /// <summary>Remise appliquée sur l'ensemble du ticket, en DZD.</summary>
    public decimal RemiseGlobale { get; init; }

    public string? Notes { get; init; }

    public IReadOnlyList<LigneVenteDemande> Lignes { get; init; } = Array.Empty<LigneVenteDemande>();

    public IReadOnlyList<PaiementDemande> Paiements { get; init; } = Array.Empty<PaiementDemande>();
}

/// <summary>Article figurant sur un ticket enregistré.</summary>
public record LigneVenteDto
{
    public int Id { get; init; }

    public int VarianteProduitId { get; init; }

    public string Designation { get; init; } = string.Empty;

    public string Sku { get; init; } = string.Empty;

    public string? CodeBarres { get; init; }

    public string Couleur { get; init; } = string.Empty;

    public string Taille { get; init; } = string.Empty;

    public int Quantite { get; init; }

    public decimal PrixUnitaire { get; init; }

    public decimal Remise { get; init; }

    public decimal Total { get; init; }

    public int QuantiteRetournee { get; init; }

    public int QuantiteRetournable => Quantite - QuantiteRetournee;
}

/// <summary>Règlement enregistré sur une vente.</summary>
public record PaiementDto
{
    public int Id { get; init; }

    public ModePaiement ModePaiement { get; init; }

    public string LibelleModePaiement { get; init; } = string.Empty;

    public decimal Montant { get; init; }

    public DateTime DatePaiement { get; init; }

    public string? Reference { get; init; }
}

/// <summary>Vente complète, telle qu'affichée à l'écran ou imprimée sur un ticket.</summary>
public record VenteDto
{
    public int Id { get; init; }

    public string NumeroVente { get; init; } = string.Empty;

    public int? ClientId { get; init; }

    public string? ClientNom { get; init; }

    public string? ClientTelephone { get; init; }

    public int UtilisateurId { get; init; }

    public string UtilisateurNom { get; init; } = string.Empty;

    public DateTime DateVente { get; init; }

    public decimal SousTotal { get; init; }

    public decimal Remise { get; init; }

    public decimal Total { get; init; }

    public decimal MontantPaye { get; init; }

    public StatutVente Statut { get; init; }

    public string LibelleStatut { get; init; } = string.Empty;

    public string? Notes { get; init; }

    public IReadOnlyList<LigneVenteDto> Lignes { get; init; } = Array.Empty<LigneVenteDto>();

    public IReadOnlyList<PaiementDto> Paiements { get; init; } = Array.Empty<PaiementDto>();

    /// <summary>Nombre total d'articles vendus sur le ticket.</summary>
    public int NombreArticles => Lignes.Sum(l => l.Quantite);

    public decimal MontantRestant => Total - MontantPaye;

    /// <summary>Vrai lorsqu'il reste au moins un article susceptible d'être retourné.</summary>
    public bool PeutEtreRetournee => Statut != StatutVente.Annulee
                                     && Lignes.Any(l => l.QuantiteRetournable > 0);
}

/// <summary>Ligne de la liste des ventes, sans le détail des articles.</summary>
public record ResumeVenteDto
{
    public int Id { get; init; }

    public string NumeroVente { get; init; } = string.Empty;

    public DateTime DateVente { get; init; }

    public string? ClientNom { get; init; }

    public string UtilisateurNom { get; init; } = string.Empty;

    public int NombreArticles { get; init; }

    public decimal Total { get; init; }

    public decimal MontantPaye { get; init; }

    public StatutVente Statut { get; init; }

    public string LibelleStatut { get; init; } = string.Empty;

    public string ModesPaiement { get; init; } = string.Empty;
}

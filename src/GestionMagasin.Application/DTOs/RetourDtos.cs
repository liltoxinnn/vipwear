using GestionMagasin.Domain.Enums;

namespace GestionMagasin.Application.DTOs;

/// <summary>Article rapporté par le client.</summary>
public record LigneRetourDemande
{
    /// <summary>Ligne de la vente d'origine, qui limite la quantité reprenable.</summary>
    public int LigneVenteId { get; init; }

    public int Quantite { get; init; }

    /// <summary>
    /// État constaté. Un article revendable réintègre le stock, un article
    /// endommagé est repris mais reste hors du stock disponible.
    /// </summary>
    public EtatArticleRetour EtatArticle { get; init; } = EtatArticleRetour.Revendable;
}

/// <summary>Retour à enregistrer sur une vente existante.</summary>
public record DemandeRetour
{
    public int VenteId { get; init; }

    public string? Motif { get; init; }

    public IReadOnlyList<LigneRetourDemande> Lignes { get; init; } = Array.Empty<LigneRetourDemande>();
}

/// <summary>
/// Échange : le client rapporte des articles et en emporte d'autres.
/// Les deux opérations sont enregistrées dans une seule transaction.
/// </summary>
public record DemandeEchange
{
    public int VenteId { get; init; }

    public string? Motif { get; init; }

    /// <summary>Articles rapportés par le client.</summary>
    public IReadOnlyList<LigneRetourDemande> LignesRetournees { get; init; } = Array.Empty<LigneRetourDemande>();

    /// <summary>Articles emportés en remplacement.</summary>
    public IReadOnlyList<LigneVenteDemande> LignesNouvelles { get; init; } = Array.Empty<LigneVenteDemande>();

    /// <summary>
    /// Règlements du complément lorsque les nouveaux articles coûtent plus
    /// cher que ceux rapportés.
    /// </summary>
    public IReadOnlyList<PaiementDemande> Paiements { get; init; } = Array.Empty<PaiementDemande>();
}

/// <summary>Article repris, tel qu'enregistré.</summary>
public record LigneRetourDto
{
    public int Id { get; init; }

    public int VarianteProduitId { get; init; }

    public string Designation { get; init; } = string.Empty;

    public string Sku { get; init; } = string.Empty;

    public int Quantite { get; init; }

    public EtatArticleRetour EtatArticle { get; init; }

    public string LibelleEtatArticle { get; init; } = string.Empty;

    public bool RemisEnStock { get; init; }

    public decimal MontantRembourse { get; init; }
}

/// <summary>Retour complet.</summary>
public record RetourDto
{
    public int Id { get; init; }

    public string NumeroRetour { get; init; } = string.Empty;

    public int VenteId { get; init; }

    public string NumeroVente { get; init; } = string.Empty;

    public DateTime DateVente { get; init; }

    public int? ClientId { get; init; }

    public string? ClientNom { get; init; }

    public string UtilisateurNom { get; init; } = string.Empty;

    public DateTime DateRetour { get; init; }

    public string? Motif { get; init; }

    public decimal TotalRembourse { get; init; }

    public StatutRetour Statut { get; init; }

    public string LibelleStatut { get; init; } = string.Empty;

    public IReadOnlyList<LigneRetourDto> Lignes { get; init; } = Array.Empty<LigneRetourDto>();

    public int NombreArticles => Lignes.Sum(l => l.Quantite);
}

/// <summary>Résultat d'un échange : le retour, la nouvelle vente et le solde.</summary>
public record ResultatEchangeDto
{
    public RetourDto Retour { get; init; } = null!;

    public VenteDto NouvelleVente { get; init; } = null!;

    /// <summary>
    /// Différence entre les nouveaux articles et le montant remboursé.
    /// Positive : le client doit un complément. Négative : le magasin lui doit
    /// la différence.
    /// </summary>
    public decimal SoldeAPayer { get; init; }
}

/// <summary>Ligne de la liste des retours.</summary>
public record ResumeRetourDto
{
    public int Id { get; init; }

    public string NumeroRetour { get; init; } = string.Empty;

    public string NumeroVente { get; init; } = string.Empty;

    public DateTime DateRetour { get; init; }

    public string? ClientNom { get; init; }

    public string UtilisateurNom { get; init; } = string.Empty;

    public int NombreArticles { get; init; }

    public decimal TotalRembourse { get; init; }

    public string? Motif { get; init; }

    public StatutRetour Statut { get; init; }

    public string LibelleStatut { get; init; } = string.Empty;
}

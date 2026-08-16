namespace GestionMagasin.Application.DTOs;

/// <summary>Fiche client.</summary>
public record ClientDto
{
    public int Id { get; init; }

    public string Nom { get; init; } = string.Empty;

    public string? Prenom { get; init; }

    public string? Telephone { get; init; }

    public string? Email { get; init; }

    public string? Adresse { get; init; }

    public string? Notes { get; init; }

    public bool Actif { get; init; }

    public DateTime DateCreation { get; init; }

    /// <summary>Nombre de ventes enregistrées au nom de ce client.</summary>
    public int NombreAchats { get; init; }

    /// <summary>Montant cumulé de ses achats, hors ventes annulées.</summary>
    public decimal TotalAchats { get; init; }

    public DateTime? DernierAchat { get; init; }

    public string NomComplet => $"{Prenom} {Nom}".Trim();
}

/// <summary>Données de création ou de modification d'un client.</summary>
public record DemandeClient
{
    public string Nom { get; init; } = string.Empty;

    public string? Prenom { get; init; }

    public string? Telephone { get; init; }

    public string? Email { get; init; }

    public string? Adresse { get; init; }

    public string? Notes { get; init; }
}

/// <summary>Fiche fournisseur.</summary>
public record FournisseurDto
{
    public int Id { get; init; }

    public string Nom { get; init; } = string.Empty;

    public string? Entreprise { get; init; }

    public string? Telephone { get; init; }

    public string? Email { get; init; }

    public string? Adresse { get; init; }

    public string? Notes { get; init; }

    public bool Actif { get; init; }

    public DateTime DateCreation { get; init; }

    public int NombreCommandes { get; init; }

    public decimal TotalCommandes { get; init; }

    /// <summary>Somme restant due à ce fournisseur.</summary>
    public decimal SoldeDu { get; init; }
}

/// <summary>Données de création ou de modification d'un fournisseur.</summary>
public record DemandeFournisseur
{
    public string Nom { get; init; } = string.Empty;

    public string? Entreprise { get; init; }

    public string? Telephone { get; init; }

    public string? Email { get; init; }

    public string? Adresse { get; init; }

    public string? Notes { get; init; }
}

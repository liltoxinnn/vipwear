namespace GestionMagasin.Application.DTOs;

/// <summary>Compte utilisateur affiché dans l'écran « Utilisateurs ».</summary>
public record UtilisateurDto
{
    public int Id { get; init; }

    public string Nom { get; init; } = string.Empty;

    public string Prenom { get; init; } = string.Empty;

    public string NomUtilisateur { get; init; } = string.Empty;

    public int RoleId { get; init; }

    public string Role { get; init; } = string.Empty;

    public bool Actif { get; init; }

    public DateTime DateCreation { get; init; }

    public DateTime? DerniereConnexion { get; init; }

    /// <summary>Nombre de ventes encaissées par ce compte.</summary>
    public int NombreVentes { get; init; }

    public string NomComplet => $"{Prenom} {Nom}".Trim();
}

/// <summary>Données de création ou de modification d'un compte.</summary>
public record DemandeUtilisateur
{
    public string Nom { get; init; } = string.Empty;

    public string Prenom { get; init; } = string.Empty;

    public string NomUtilisateur { get; init; } = string.Empty;

    /// <summary>Obligatoire à la création, facultatif lors d'une modification.</summary>
    public string? MotDePasse { get; init; }

    public int RoleId { get; init; }
}

/// <summary>Rôle et permissions qui lui sont attribuées.</summary>
public record RoleDto
{
    public int Id { get; init; }

    public string Nom { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool EstSysteme { get; init; }

    public int NombreUtilisateurs { get; init; }

    public IReadOnlyList<PermissionDto> Permissions { get; init; } = Array.Empty<PermissionDto>();
}

/// <summary>Permission élémentaire.</summary>
public record PermissionDto
{
    public int Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Nom { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string Categorie { get; init; } = string.Empty;

    /// <summary>Vrai lorsque le rôle consulté possède cette permission.</summary>
    public bool Accordee { get; init; }
}

/// <summary>Entrée du journal d'audit.</summary>
public record JournalAuditDto
{
    public int Id { get; init; }

    public string? Utilisateur { get; init; }

    public string Action { get; init; } = string.Empty;

    public string TypeEntite { get; init; } = string.Empty;

    public int? EntiteId { get; init; }

    public string Description { get; init; } = string.Empty;

    public DateTime DateCreation { get; init; }
}

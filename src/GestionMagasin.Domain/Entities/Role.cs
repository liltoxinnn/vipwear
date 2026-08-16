using GestionMagasin.Domain.Common;

namespace GestionMagasin.Domain.Entities;

/// <summary>
/// Rôle attribué à un utilisateur. Un rôle porte un ensemble de permissions,
/// ce qui permet d'ajouter de nouveaux profils sans modifier le code.
/// </summary>
public class Role : EntiteBase
{
    public string Nom { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Rôle livré avec le logiciel : il ne peut pas être supprimé afin de
    /// garantir qu'il reste toujours un profil administrateur opérationnel.
    /// </summary>
    public bool EstSysteme { get; set; }

    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();

    public ICollection<Utilisateur> Utilisateurs { get; set; } = new List<Utilisateur>();
}

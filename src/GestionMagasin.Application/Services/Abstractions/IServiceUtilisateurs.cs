using GestionMagasin.Application.DTOs;

namespace GestionMagasin.Application.Services.Abstractions;

/// <summary>Gestion des comptes, des rôles et des permissions.</summary>
public interface IServiceUtilisateurs
{
    Task<UtilisateurDto> CreerAsync(DemandeUtilisateur demande, CancellationToken jeton = default);

    Task<UtilisateurDto> ModifierAsync(int utilisateurId, DemandeUtilisateur demande, CancellationToken jeton = default);

    /// <summary>
    /// Active ou désactive un compte. Le logiciel refuse de désactiver le
    /// dernier administrateur actif, pour ne jamais laisser le magasin sans
    /// personne capable d'administrer les comptes.
    /// </summary>
    Task<UtilisateurDto> DefinirEtatAsync(int utilisateurId, bool actif, CancellationToken jeton = default);

    /// <summary>Attribue un nouveau mot de passe à un compte, sans connaître l'ancien.</summary>
    Task ReinitialiserMotDePasseAsync(int utilisateurId, string nouveauMotDePasse, CancellationToken jeton = default);

    Task<UtilisateurDto?> ObtenirAsync(int utilisateurId, CancellationToken jeton = default);

    Task<IReadOnlyList<UtilisateurDto>> ListerAsync(
        bool inclureInactifs = true,
        CancellationToken jeton = default);

    // --- Rôles et permissions ---

    Task<IReadOnlyList<RoleDto>> ListerRolesAsync(CancellationToken jeton = default);

    Task<RoleDto?> ObtenirRoleAsync(int roleId, CancellationToken jeton = default);

    /// <summary>Toutes les permissions du logiciel, marquées selon le rôle consulté.</summary>
    Task<IReadOnlyList<PermissionDto>> ListerPermissionsAsync(int? roleId = null, CancellationToken jeton = default);

    Task<RoleDto> EnregistrerRoleAsync(
        int? roleId,
        string nom,
        string? description,
        CancellationToken jeton = default);

    /// <summary>Remplace la liste des permissions d'un rôle.</summary>
    Task<RoleDto> DefinirPermissionsAsync(
        int roleId,
        IReadOnlyList<string> codesPermissions,
        CancellationToken jeton = default);

    Task SupprimerRoleAsync(int roleId, CancellationToken jeton = default);

    // --- Journal d'audit ---

    Task<IReadOnlyList<JournalAuditDto>> ConsulterJournalAsync(
        DateTime? debut = null,
        DateTime? fin = null,
        int? utilisateurId = null,
        string? action = null,
        string? recherche = null,
        int limite = 500,
        CancellationToken jeton = default);
}

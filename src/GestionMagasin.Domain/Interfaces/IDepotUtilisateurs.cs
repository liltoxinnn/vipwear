using GestionMagasin.Domain.Entities;

namespace GestionMagasin.Domain.Interfaces;

/// <summary>Accès aux comptes utilisateurs et à leurs autorisations.</summary>
public interface IDepotUtilisateurs : IDepotGenerique<Utilisateur>
{
    /// <summary>
    /// Charge un compte à partir de son identifiant de connexion, avec son
    /// rôle et les permissions associées.
    /// </summary>
    Task<Utilisateur?> ObtenirParNomUtilisateurAsync(string nomUtilisateur, CancellationToken jeton = default);

    /// <summary>Charge un compte avec son rôle et ses permissions.</summary>
    Task<Utilisateur?> ObtenirAvecRoleAsync(int id, CancellationToken jeton = default);

    /// <summary>Indique si un identifiant de connexion est déjà utilisé.</summary>
    Task<bool> NomUtilisateurExisteAsync(string nomUtilisateur, int? idAExclure = null, CancellationToken jeton = default);

    /// <summary>Codes des permissions accordées à un rôle.</summary>
    Task<IReadOnlyList<string>> ObtenirPermissionsDuRoleAsync(int roleId, CancellationToken jeton = default);

    /// <summary>
    /// Nombre d'administrateurs encore actifs. Sert à interdire la
    /// désactivation du dernier administrateur du magasin.
    /// </summary>
    Task<int> CompterAdministrateursActifsAsync(int? idAExclure = null, CancellationToken jeton = default);
}

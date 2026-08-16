using GestionMagasin.Domain.Entities;
using GestionMagasin.Domain.Interfaces;
using GestionMagasin.Domain.Securite;
using GestionMagasin.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GestionMagasin.Infrastructure.Repositories;

/// <summary>Accès aux comptes utilisateurs et à leurs autorisations.</summary>
public class DepotUtilisateurs : DepotGenerique<Utilisateur>, IDepotUtilisateurs
{
    public DepotUtilisateurs(ContexteMagasin contexte) : base(contexte)
    {
    }

    public Task<Utilisateur?> ObtenirParNomUtilisateurAsync(string nomUtilisateur, CancellationToken jeton = default)
    {
        var recherche = nomUtilisateur.Trim();

        // La comparaison est insensible à la casse : « Ahmed » et « ahmed »
        // désignent le même compte.
        return Ensemble.AsNoTracking()
            .Include(u => u.Role).ThenInclude(r => r.Permissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.NomUtilisateur.ToLower() == recherche.ToLower(), jeton);
    }

    public Task<Utilisateur?> ObtenirAvecRoleAsync(int id, CancellationToken jeton = default) =>
        Ensemble.AsNoTracking()
            .Include(u => u.Role).ThenInclude(r => r.Permissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == id, jeton);

    public Task<bool> NomUtilisateurExisteAsync(
        string nomUtilisateur,
        int? idAExclure = null,
        CancellationToken jeton = default)
    {
        var recherche = nomUtilisateur.Trim().ToLower();

        return Ensemble.AsNoTracking().AnyAsync(
            u => u.NomUtilisateur.ToLower() == recherche && (idAExclure == null || u.Id != idAExclure),
            jeton);
    }

    public async Task<IReadOnlyList<string>> ObtenirPermissionsDuRoleAsync(int roleId, CancellationToken jeton = default) =>
        await Contexte.RolePermissions.AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission.Code)
            .ToListAsync(jeton)
            .ConfigureAwait(false);

    public Task<int> CompterAdministrateursActifsAsync(int? idAExclure = null, CancellationToken jeton = default) =>
        Ensemble.AsNoTracking().CountAsync(
            u => u.Actif
                 && u.Role.Nom == NomsRoles.Administrateur
                 && (idAExclure == null || u.Id != idAExclure),
            jeton);
}

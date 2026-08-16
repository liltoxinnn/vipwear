using GestionMagasin.Application.Common;
using GestionMagasin.Application.DTOs;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Application.Validators;
using GestionMagasin.Domain.Entities;
using GestionMagasin.Domain.Exceptions;
using GestionMagasin.Domain.Interfaces;
using GestionMagasin.Domain.Securite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.Application.Services;

/// <inheritdoc cref="IServiceUtilisateurs"/>
public class ServiceUtilisateurs : IServiceUtilisateurs
{
    private readonly IUniteDeTravail _uniteDeTravail;
    private readonly IHacheurMotDePasse _hacheur;
    private readonly IServiceAudit _audit;
    private readonly ISessionUtilisateur _session;
    private readonly IFournisseurHorodatage _horodatage;
    private readonly ILogger<ServiceUtilisateurs> _journal;

    public ServiceUtilisateurs(
        IUniteDeTravail uniteDeTravail,
        IHacheurMotDePasse hacheur,
        IServiceAudit audit,
        ISessionUtilisateur session,
        IFournisseurHorodatage horodatage,
        ILogger<ServiceUtilisateurs> journal)
    {
        _uniteDeTravail = uniteDeTravail;
        _hacheur = hacheur;
        _audit = audit;
        _session = session;
        _horodatage = horodatage;
        _journal = journal;
    }

    // ==================================================================
    // Comptes
    // ==================================================================

    public async Task<UtilisateurDto> CreerAsync(DemandeUtilisateur demande, CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.GererUtilisateurs);

        var erreurs = new List<string>();

        if (string.IsNullOrWhiteSpace(demande.Nom))
        {
            erreurs.Add("Le nom est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(demande.Prenom))
        {
            erreurs.Add("Le prénom est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(demande.NomUtilisateur))
        {
            erreurs.Add("L'identifiant de connexion est obligatoire.");
        }

        if (string.IsNullOrWhiteSpace(demande.MotDePasse))
        {
            erreurs.Add("Le mot de passe est obligatoire à la création d'un compte.");
        }
        else
        {
            erreurs.AddRange(ValidateurMotDePasse.Controler(demande.MotDePasse));
        }

        if (erreurs.Count > 0)
        {
            throw new ValidationMetierException(erreurs);
        }

        if (await _uniteDeTravail.Utilisateurs
                .NomUtilisateurExisteAsync(demande.NomUtilisateur, null, jeton).ConfigureAwait(false))
        {
            throw new ValidationMetierException(
                $"L'identifiant « {demande.NomUtilisateur.Trim()} » est déjà utilisé.");
        }

        await ControlerRoleAsync(demande.RoleId, jeton).ConfigureAwait(false);

        var utilisateur = new Utilisateur
        {
            Nom = demande.Nom.Trim(),
            Prenom = demande.Prenom.Trim(),
            NomUtilisateur = demande.NomUtilisateur.Trim(),
            PasswordHash = _hacheur.Hacher(demande.MotDePasse!),
            RoleId = demande.RoleId,
            Actif = true,
            DateCreation = _horodatage.MaintenantUtc
        };

        await _uniteDeTravail.Depot<Utilisateur>().AjouterAsync(utilisateur, jeton).ConfigureAwait(false);
        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        await _audit.TracerEtEnregistrerAsync(
            ActionsAudit.Creation,
            nameof(Utilisateur),
            utilisateur.Id,
            $"Compte « {utilisateur.NomUtilisateur} » créé pour {utilisateur.NomComplet}.",
            jeton).ConfigureAwait(false);

        _journal.LogInformation("Compte {Identifiant} créé.", utilisateur.NomUtilisateur);

        return await ObtenirObligatoireAsync(utilisateur.Id, jeton).ConfigureAwait(false);
    }

    public async Task<UtilisateurDto> ModifierAsync(
        int utilisateurId,
        DemandeUtilisateur demande,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.GererUtilisateurs);

        var utilisateur = await _uniteDeTravail.Depot<Utilisateur>().RequeteSuivie()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == utilisateurId, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Utilisateur", utilisateurId);

        if (string.IsNullOrWhiteSpace(demande.Nom) || string.IsNullOrWhiteSpace(demande.Prenom))
        {
            throw new ValidationMetierException("Le nom et le prénom sont obligatoires.");
        }

        if (await _uniteDeTravail.Utilisateurs
                .NomUtilisateurExisteAsync(demande.NomUtilisateur, utilisateurId, jeton).ConfigureAwait(false))
        {
            throw new ValidationMetierException(
                $"L'identifiant « {demande.NomUtilisateur.Trim()} » est déjà utilisé.");
        }

        await ControlerRoleAsync(demande.RoleId, jeton).ConfigureAwait(false);

        // Retirer le rôle d'administrateur au dernier administrateur actif
        // rendrait le logiciel inadministrable.
        if (demande.RoleId != utilisateur.RoleId
            && utilisateur.Role.Nom == NomsRoles.Administrateur
            && utilisateur.Actif)
        {
            await ExigerAutreAdministrateurAsync(
                utilisateurId,
                "Ce compte est le dernier administrateur actif : son rôle ne peut pas être modifié.",
                jeton).ConfigureAwait(false);
        }

        utilisateur.Nom = demande.Nom.Trim();
        utilisateur.Prenom = demande.Prenom.Trim();
        utilisateur.NomUtilisateur = demande.NomUtilisateur.Trim();
        utilisateur.RoleId = demande.RoleId;

        if (!string.IsNullOrWhiteSpace(demande.MotDePasse))
        {
            ValidateurMotDePasse.Valider(demande.MotDePasse);
            utilisateur.PasswordHash = _hacheur.Hacher(demande.MotDePasse);
        }

        await _audit.TracerAsync(
            ActionsAudit.Modification,
            nameof(Utilisateur),
            utilisateur.Id,
            $"Compte « {utilisateur.NomUtilisateur} » modifié.",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        return await ObtenirObligatoireAsync(utilisateurId, jeton).ConfigureAwait(false);
    }

    public async Task<UtilisateurDto> DefinirEtatAsync(
        int utilisateurId,
        bool actif,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.GererUtilisateurs);

        var utilisateur = await _uniteDeTravail.Depot<Utilisateur>().RequeteSuivie()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == utilisateurId, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Utilisateur", utilisateurId);

        if (!actif)
        {
            if (utilisateurId == _session.UtilisateurId)
            {
                throw new ValidationMetierException("Vous ne pouvez pas désactiver votre propre compte.");
            }

            if (utilisateur.Role.Nom == NomsRoles.Administrateur)
            {
                await ExigerAutreAdministrateurAsync(
                    utilisateurId,
                    "Ce compte est le dernier administrateur actif : il ne peut pas être désactivé.",
                    jeton).ConfigureAwait(false);
            }
        }

        utilisateur.Actif = actif;

        await _audit.TracerAsync(
            actif ? ActionsAudit.Activation : ActionsAudit.Desactivation,
            nameof(Utilisateur),
            utilisateur.Id,
            actif
                ? $"Compte « {utilisateur.NomUtilisateur} » réactivé."
                : $"Compte « {utilisateur.NomUtilisateur} » désactivé.",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        return await ObtenirObligatoireAsync(utilisateurId, jeton).ConfigureAwait(false);
    }

    public async Task ReinitialiserMotDePasseAsync(
        int utilisateurId,
        string nouveauMotDePasse,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.GererUtilisateurs);

        ValidateurMotDePasse.Valider(nouveauMotDePasse);

        var utilisateur = await _uniteDeTravail.Depot<Utilisateur>().RequeteSuivie()
            .FirstOrDefaultAsync(u => u.Id == utilisateurId, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Utilisateur", utilisateurId);

        utilisateur.PasswordHash = _hacheur.Hacher(nouveauMotDePasse);

        await _audit.TracerAsync(
            ActionsAudit.ChangementMotDePasse,
            nameof(Utilisateur),
            utilisateur.Id,
            $"Mot de passe du compte « {utilisateur.NomUtilisateur} » réinitialisé par un administrateur.",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        _journal.LogWarning(
            "Mot de passe du compte {Identifiant} réinitialisé par {Administrateur}.",
            utilisateur.NomUtilisateur, _session.NomUtilisateur);
    }

    public async Task<UtilisateurDto?> ObtenirAsync(int utilisateurId, CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.GererUtilisateurs);

        return await ConstruireProjection()
            .FirstOrDefaultAsync(u => u.Id == utilisateurId, jeton)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UtilisateurDto>> ListerAsync(
        bool inclureInactifs = true,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.GererUtilisateurs);

        var requete = _uniteDeTravail.Depot<Utilisateur>().Requete();

        if (!inclureInactifs)
        {
            requete = requete.Where(u => u.Actif);
        }

        return await ConstruireProjection(requete)
            .OrderBy(u => u.Nom)
            .ThenBy(u => u.Prenom)
            .ToListAsync(jeton)
            .ConfigureAwait(false);
    }

    // ==================================================================
    // Rôles et permissions
    // ==================================================================

    public async Task<IReadOnlyList<RoleDto>> ListerRolesAsync(CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.GererUtilisateurs);

        return await _uniteDeTravail.Depot<Role>().Requete()
            .OrderBy(r => r.Nom)
            .Select(r => new RoleDto
            {
                Id = r.Id,
                Nom = r.Nom,
                Description = r.Description,
                EstSysteme = r.EstSysteme,
                NombreUtilisateurs = r.Utilisateurs.Count,
                Permissions = r.Permissions.Select(rp => new PermissionDto
                {
                    Id = rp.Permission.Id,
                    Code = rp.Permission.Code,
                    Nom = rp.Permission.Nom,
                    Description = rp.Permission.Description,
                    Categorie = rp.Permission.Categorie,
                    Accordee = true
                }).ToList()
            })
            .ToListAsync(jeton)
            .ConfigureAwait(false);
    }

    public async Task<RoleDto?> ObtenirRoleAsync(int roleId, CancellationToken jeton = default)
    {
        var roles = await ListerRolesAsync(jeton).ConfigureAwait(false);

        return roles.FirstOrDefault(r => r.Id == roleId);
    }

    public async Task<IReadOnlyList<PermissionDto>> ListerPermissionsAsync(
        int? roleId = null,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.GererUtilisateurs);

        var accordees = roleId.HasValue
            ? (await _uniteDeTravail.Utilisateurs
                .ObtenirPermissionsDuRoleAsync(roleId.Value, jeton).ConfigureAwait(false)).ToHashSet()
            : new HashSet<string>();

        var permissions = await _uniteDeTravail.Depot<Permission>().Requete()
            .OrderBy(p => p.Categorie)
            .ThenBy(p => p.Nom)
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        return permissions.Select(p => new PermissionDto
        {
            Id = p.Id,
            Code = p.Code,
            Nom = p.Nom,
            Description = p.Description,
            Categorie = p.Categorie,
            Accordee = accordees.Contains(p.Code)
        }).ToList();
    }

    public async Task<RoleDto> EnregistrerRoleAsync(
        int? roleId,
        string nom,
        string? description,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.GererUtilisateurs);

        if (string.IsNullOrWhiteSpace(nom))
        {
            throw new ValidationMetierException("Le nom du rôle est obligatoire.");
        }

        var nomNettoye = nom.Trim();

        var doublon = await _uniteDeTravail.Depot<Role>().Requete()
            .AnyAsync(r => r.Nom.ToLower() == nomNettoye.ToLower() && (roleId == null || r.Id != roleId), jeton)
            .ConfigureAwait(false);

        if (doublon)
        {
            throw new ValidationMetierException($"Le rôle « {nomNettoye} » existe déjà.");
        }

        Role role;

        if (roleId.HasValue)
        {
            role = await _uniteDeTravail.Depot<Role>().RequeteSuivie()
                .FirstOrDefaultAsync(r => r.Id == roleId.Value, jeton)
                .ConfigureAwait(false)
                ?? throw new EntiteIntrouvableException("Rôle", roleId.Value);

            // Renommer un rôle système romprait les contrôles qui s'appuient
            // sur son nom (dernier administrateur, notamment).
            if (role.EstSysteme && !string.Equals(role.Nom, nomNettoye, StringComparison.Ordinal))
            {
                throw new ValidationMetierException(
                    $"Le rôle « {role.Nom} » est un rôle du système : son nom ne peut pas être modifié. " +
                    "Vous pouvez en revanche ajuster ses permissions.");
            }

            role.Nom = nomNettoye;
            role.Description = description?.Trim();
        }
        else
        {
            role = new Role { Nom = nomNettoye, Description = description?.Trim(), EstSysteme = false };
            await _uniteDeTravail.Depot<Role>().AjouterAsync(role, jeton).ConfigureAwait(false);
        }

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);

        await _audit.TracerEtEnregistrerAsync(
            roleId.HasValue ? ActionsAudit.Modification : ActionsAudit.Creation,
            nameof(Role),
            role.Id,
            $"Rôle « {role.Nom} » {(roleId.HasValue ? "modifié" : "créé")}.",
            jeton).ConfigureAwait(false);

        return await ObtenirRoleAsync(role.Id, jeton).ConfigureAwait(false)
               ?? throw new EntiteIntrouvableException("Rôle", role.Id);
    }

    public async Task<RoleDto> DefinirPermissionsAsync(
        int roleId,
        IReadOnlyList<string> codesPermissions,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.GererUtilisateurs);

        await using var transaction = await _uniteDeTravail.DemarrerTransactionAsync(jeton).ConfigureAwait(false);

        var role = await _uniteDeTravail.Depot<Role>().RequeteSuivie()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Rôle", roleId);

        // Le rôle Administrateur doit conserver la gestion des utilisateurs,
        // faute de quoi plus personne ne pourrait rétablir les permissions.
        if (role.Nom == NomsRoles.Administrateur
            && !codesPermissions.Contains(CodesPermissions.GererUtilisateurs))
        {
            throw new ValidationMetierException(
                "Le rôle Administrateur doit conserver la permission « Gérer les utilisateurs ».");
        }

        var permissions = await _uniteDeTravail.Depot<Permission>().Requete()
            .Where(p => codesPermissions.Contains(p.Code))
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        // La collection de navigation du rôle est modifiée directement :
        // Entity Framework en déduit les lignes de liaison à supprimer et à
        // créer, sans qu'il soit nécessaire de les manipuler une à une.
        var permissionsVoulues = permissions.Select(p => p.Id).ToHashSet();

        var aRetirer = role.Permissions
            .Where(rp => !permissionsVoulues.Contains(rp.PermissionId))
            .ToList();

        foreach (var lien in aRetirer)
        {
            role.Permissions.Remove(lien);
        }

        var dejaPresentes = role.Permissions.Select(rp => rp.PermissionId).ToHashSet();

        foreach (var permissionId in permissionsVoulues.Where(id => !dejaPresentes.Contains(id)))
        {
            role.Permissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
        }

        await _audit.TracerAsync(
            ActionsAudit.Modification,
            nameof(Role),
            roleId,
            $"Permissions du rôle « {role.Nom} » mises à jour : {permissions.Count} autorisation(s).",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);
        await transaction.ValiderAsync(jeton).ConfigureAwait(false);

        _uniteDeTravail.ViderSuivi();

        return await ObtenirRoleAsync(roleId, jeton).ConfigureAwait(false)
               ?? throw new EntiteIntrouvableException("Rôle", roleId);
    }

    public async Task SupprimerRoleAsync(int roleId, CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.GererUtilisateurs);

        var role = await _uniteDeTravail.Depot<Role>().RequeteSuivie()
            .Include(r => r.Utilisateurs)
            .FirstOrDefaultAsync(r => r.Id == roleId, jeton)
            .ConfigureAwait(false)
            ?? throw new EntiteIntrouvableException("Rôle", roleId);

        if (role.EstSysteme)
        {
            throw new ValidationMetierException(
                $"Le rôle « {role.Nom} » fait partie du système et ne peut pas être supprimé.");
        }

        if (role.Utilisateurs.Count > 0)
        {
            throw new ValidationMetierException(
                $"Le rôle « {role.Nom} » est attribué à {role.Utilisateurs.Count} compte(s). " +
                "Réaffectez-les avant de supprimer ce rôle.");
        }

        _uniteDeTravail.Depot<Role>().Supprimer(role);

        await _audit.TracerAsync(
            ActionsAudit.Suppression,
            nameof(Role),
            roleId,
            $"Rôle « {role.Nom} » supprimé.",
            jeton).ConfigureAwait(false);

        await _uniteDeTravail.EnregistrerAsync(jeton).ConfigureAwait(false);
    }

    // ==================================================================
    // Journal d'audit
    // ==================================================================

    public async Task<IReadOnlyList<JournalAuditDto>> ConsulterJournalAsync(
        DateTime? debut = null,
        DateTime? fin = null,
        int? utilisateurId = null,
        string? action = null,
        string? recherche = null,
        int limite = 500,
        CancellationToken jeton = default)
    {
        _session.ExigerPermission(CodesPermissions.VoirJournalAudit);

        var requete = _uniteDeTravail.Depot<JournalAudit>().Requete();

        if (debut.HasValue)
        {
            requete = requete.Where(j => j.DateCreation >= debut.Value);
        }

        if (fin.HasValue)
        {
            requete = requete.Where(j => j.DateCreation <= fin.Value);
        }

        if (utilisateurId.HasValue)
        {
            requete = requete.Where(j => j.UtilisateurId == utilisateurId.Value);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            requete = requete.Where(j => j.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(recherche))
        {
            var terme = recherche.Trim().ToLowerInvariant();
            requete = requete.Where(j => j.Description.ToLower().Contains(terme));
        }

        return await requete
            .OrderByDescending(j => j.DateCreation)
            .ThenByDescending(j => j.Id)
            .Take(limite)
            .Select(j => new JournalAuditDto
            {
                Id = j.Id,
                Utilisateur = j.Utilisateur != null ? j.Utilisateur.Prenom + " " + j.Utilisateur.Nom : null,
                Action = j.Action,
                TypeEntite = j.TypeEntite,
                EntiteId = j.EntiteId,
                Description = j.Description,
                DateCreation = j.DateCreation
            })
            .ToListAsync(jeton)
            .ConfigureAwait(false);
    }

    // ==================================================================
    // Utilitaires internes
    // ==================================================================

    private IQueryable<UtilisateurDto> ConstruireProjection(IQueryable<Utilisateur>? source = null) =>
        (source ?? _uniteDeTravail.Depot<Utilisateur>().Requete())
        .Select(u => new UtilisateurDto
        {
            Id = u.Id,
            Nom = u.Nom,
            Prenom = u.Prenom,
            NomUtilisateur = u.NomUtilisateur,
            RoleId = u.RoleId,
            Role = u.Role.Nom,
            Actif = u.Actif,
            DateCreation = u.DateCreation,
            DerniereConnexion = u.DerniereConnexion,
            NombreVentes = _uniteDeTravail.Depot<Vente>().Requete().Count(v => v.UtilisateurId == u.Id)
        });

    private async Task<UtilisateurDto> ObtenirObligatoireAsync(int utilisateurId, CancellationToken jeton)
    {
        _uniteDeTravail.ViderSuivi();

        return await ObtenirAsync(utilisateurId, jeton).ConfigureAwait(false)
               ?? throw new EntiteIntrouvableException("Utilisateur", utilisateurId);
    }

    private async Task ControlerRoleAsync(int roleId, CancellationToken jeton)
    {
        var existe = await _uniteDeTravail.Depot<Role>().Requete()
            .AnyAsync(r => r.Id == roleId, jeton)
            .ConfigureAwait(false);

        if (!existe)
        {
            throw new ValidationMetierException("Le rôle sélectionné n'existe pas.");
        }
    }

    private async Task ExigerAutreAdministrateurAsync(int utilisateurId, string message, CancellationToken jeton)
    {
        var autres = await _uniteDeTravail.Utilisateurs
            .CompterAdministrateursActifsAsync(utilisateurId, jeton)
            .ConfigureAwait(false);

        if (autres == 0)
        {
            throw new ValidationMetierException(message);
        }
    }
}

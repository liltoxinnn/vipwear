using GestionMagasin.Domain.Entities;
using GestionMagasin.Domain.Interfaces;
using GestionMagasin.Domain.Securite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GestionMagasin.Infrastructure.Data;

/// <summary>
/// Prépare la base au démarrage : applique les migrations en attente puis
/// garantit la présence des données de référence indispensables au
/// fonctionnement du logiciel (permissions, rôles, tailles, couleurs,
/// fiche magasin et compte administrateur).
/// Toutes les étapes sont idempotentes : relancer l'initialisation ne crée
/// jamais de doublon et n'écrase jamais une donnée saisie par le magasin.
/// </summary>
public class InitialiseurBaseDonnees
{
    /// <summary>Identifiant du compte créé à la toute première installation.</summary>
    public const string NomUtilisateurAdministrateurParDefaut = ComptesParDefaut.NomUtilisateurAdministrateur;

    /// <summary>
    /// Mot de passe initial du compte administrateur. Le logiciel impose son
    /// changement à la première connexion.
    /// </summary>
    public const string MotDePasseAdministrateurParDefaut = ComptesParDefaut.MotDePasseAdministrateur;

    private readonly ContexteMagasin _contexte;
    private readonly IHacheurMotDePasse _hacheur;
    private readonly IFournisseurHorodatage _horodatage;
    private readonly ILogger<InitialiseurBaseDonnees> _journal;

    public InitialiseurBaseDonnees(
        ContexteMagasin contexte,
        IHacheurMotDePasse hacheur,
        IFournisseurHorodatage horodatage,
        ILogger<InitialiseurBaseDonnees> journal)
    {
        _contexte = contexte;
        _hacheur = hacheur;
        _horodatage = horodatage;
        _journal = journal;
    }

    /// <summary>Applique les migrations puis amorce les données de référence.</summary>
    public async Task InitialiserAsync(CancellationToken jeton = default)
    {
        await MigrerAsync(jeton).ConfigureAwait(false);
        await AmorcerAsync(jeton).ConfigureAwait(false);
    }

    /// <summary>Applique les migrations Entity Framework encore en attente.</summary>
    public async Task MigrerAsync(CancellationToken jeton = default)
    {
        var enAttente = await _contexte.Database.GetPendingMigrationsAsync(jeton).ConfigureAwait(false);
        var liste = enAttente.ToList();

        if (liste.Count == 0)
        {
            _journal.LogInformation("La base de données est déjà à jour.");
            return;
        }

        _journal.LogInformation(
            "Application de {Nombre} migration(s) : {Migrations}",
            liste.Count,
            string.Join(", ", liste));

        await _contexte.Database.MigrateAsync(jeton).ConfigureAwait(false);
    }

    /// <summary>Crée les données de référence manquantes.</summary>
    public async Task AmorcerAsync(CancellationToken jeton = default)
    {
        await AmorcerPermissionsAsync(jeton).ConfigureAwait(false);
        await AmorcerRolesAsync(jeton).ConfigureAwait(false);
        await AmorcerTaillesAsync(jeton).ConfigureAwait(false);
        await AmorcerCouleursAsync(jeton).ConfigureAwait(false);
        await AmorcerParametresAsync(jeton).ConfigureAwait(false);
        await AmorcerAdministrateurAsync(jeton).ConfigureAwait(false);
    }

    private async Task AmorcerPermissionsAsync(CancellationToken jeton)
    {
        var existantes = await _contexte.Permissions
            .Select(p => p.Code)
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        var manquantes = CataloguePermissions.Definitions
            .Where(d => !existantes.Contains(d.Code))
            .Select(d => new Permission
            {
                Code = d.Code,
                Nom = d.Libelle,
                Description = d.Description,
                Categorie = d.Categorie
            })
            .ToList();

        if (manquantes.Count == 0)
        {
            return;
        }

        _contexte.Permissions.AddRange(manquantes);
        await _contexte.SaveChangesAsync(jeton).ConfigureAwait(false);

        _journal.LogInformation("{Nombre} permission(s) ajoutée(s).", manquantes.Count);
    }

    private async Task AmorcerRolesAsync(CancellationToken jeton)
    {
        var permissionsParCode = await _contexte.Permissions
            .ToDictionaryAsync(p => p.Code, p => p.Id, jeton)
            .ConfigureAwait(false);

        foreach (var definition in CataloguePermissions.RolesParDefaut)
        {
            var role = await _contexte.Roles
                .Include(r => r.Permissions)
                .FirstOrDefaultAsync(r => r.Nom == definition.Nom, jeton)
                .ConfigureAwait(false);

            if (role is null)
            {
                role = new Role
                {
                    Nom = definition.Nom,
                    Description = definition.Description,
                    EstSysteme = true
                };

                _contexte.Roles.Add(role);
                await _contexte.SaveChangesAsync(jeton).ConfigureAwait(false);

                _journal.LogInformation("Rôle « {Role} » créé.", definition.Nom);
            }

            // Les permissions d'un rôle système sont complétées si de
            // nouvelles autorisations apparaissent dans une mise à jour, mais
            // celles retirées volontairement par l'administrateur ne sont
            // jamais réattribuées de force.
            if (role.Permissions.Count > 0)
            {
                continue;
            }

            var aAjouter = definition.Permissions
                .Where(permissionsParCode.ContainsKey)
                .Select(code => new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permissionsParCode[code]
                })
                .ToList();

            if (aAjouter.Count == 0)
            {
                continue;
            }

            _contexte.RolePermissions.AddRange(aAjouter);
            await _contexte.SaveChangesAsync(jeton).ConfigureAwait(false);

            _journal.LogInformation(
                "{Nombre} permission(s) attribuée(s) au rôle « {Role} ».",
                aAjouter.Count,
                definition.Nom);
        }
    }

    private async Task AmorcerTaillesAsync(CancellationToken jeton)
    {
        if (await _contexte.Tailles.AnyAsync(jeton).ConfigureAwait(false))
        {
            return;
        }

        string[] tailles = ["XS", "S", "M", "L", "XL", "XXL", "XXXL"];

        _contexte.Tailles.AddRange(tailles.Select((nom, index) => new Taille
        {
            Nom = nom,
            Ordre = (index + 1) * 10,
            Actif = true
        }));

        await _contexte.SaveChangesAsync(jeton).ConfigureAwait(false);
        _journal.LogInformation("Tailles standard créées.");
    }

    private async Task AmorcerCouleursAsync(CancellationToken jeton)
    {
        if (await _contexte.Couleurs.AnyAsync(jeton).ConfigureAwait(false))
        {
            return;
        }

        (string Nom, string Code)[] couleurs =
        [
            ("Noir", "#000000"),
            ("Blanc", "#FFFFFF"),
            ("Gris", "#9E9E9E"),
            ("Rouge", "#D32F2F"),
            ("Bleu", "#1976D2"),
            ("Vert", "#388E3C"),
            ("Jaune", "#FBC02D"),
            ("Beige", "#D7CCC8"),
            ("Marron", "#5D4037"),
            ("Rose", "#EC407A")
        ];

        _contexte.Couleurs.AddRange(couleurs.Select(c => new Couleur
        {
            Nom = c.Nom,
            CodeCouleur = c.Code,
            Actif = true
        }));

        await _contexte.SaveChangesAsync(jeton).ConfigureAwait(false);
        _journal.LogInformation("Couleurs standard créées.");
    }

    private async Task AmorcerParametresAsync(CancellationToken jeton)
    {
        if (await _contexte.ParametresMagasin.AnyAsync(jeton).ConfigureAwait(false))
        {
            return;
        }

        _contexte.ParametresMagasin.Add(new ParametresMagasin
        {
            Id = 1,
            NomMagasin = "Mon Magasin de Vêtements",
            Devise = "DZD",
            SymboleDevise = "DA",
            InformationsTicket = "Merci de votre visite et à bientôt !",
            TauxTva = 19m,
            DelaiRetourJours = 30,
            DateModification = _horodatage.MaintenantUtc
        });

        await _contexte.SaveChangesAsync(jeton).ConfigureAwait(false);
        _journal.LogInformation("Fiche magasin initialisée avec la devise DZD.");
    }

    private async Task AmorcerAdministrateurAsync(CancellationToken jeton)
    {
        if (await _contexte.Utilisateurs.AnyAsync(jeton).ConfigureAwait(false))
        {
            return;
        }

        var roleAdministrateur = await _contexte.Roles
            .FirstAsync(r => r.Nom == NomsRoles.Administrateur, jeton)
            .ConfigureAwait(false);

        _contexte.Utilisateurs.Add(new Utilisateur
        {
            Nom = "Administrateur",
            Prenom = "Compte",
            NomUtilisateur = NomUtilisateurAdministrateurParDefaut,
            PasswordHash = _hacheur.Hacher(MotDePasseAdministrateurParDefaut),
            RoleId = roleAdministrateur.Id,
            Actif = true,
            DateCreation = _horodatage.MaintenantUtc
        });

        await _contexte.SaveChangesAsync(jeton).ConfigureAwait(false);

        _journal.LogWarning(
            "Compte administrateur « {Compte} » créé avec le mot de passe initial. " +
            "Il doit être modifié dès la première connexion.",
            NomUtilisateurAdministrateurParDefaut);
    }
}

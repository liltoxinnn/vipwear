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
        await AmorcerSystemesTaillesAsync(jeton).ConfigureAwait(false);
        await AmorcerCategoriesAsync(jeton).ConfigureAwait(false);
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

    /// <summary>
    /// Crée les systèmes de tailles et leurs tailles.
    ///
    /// Une chemise se vend en S, M, L ; un pantalon en 38, 40, 42 ; une
    /// chaussure en pointures. Mélanger les trois séries à la création d'un
    /// article laisserait créer une chaussure en XXL — une déclinaison que
    /// personne ne vendra et qui restera dans le stock.
    ///
    /// Chaque système manquant est ajouté, puis ses tailles manquantes : un
    /// magasin qui a renommé ou supprimé une taille ne la voit pas revenir,
    /// et une mise à jour du logiciel peut enrichir la liste.
    /// </summary>
    private async Task AmorcerSystemesTaillesAsync(CancellationToken jeton)
    {
        (string Systeme, int Ordre, string[] Tailles)[] catalogue =
        [
            ("Tailles vêtements (XS à XXXL)", 10,
                ["XS", "S", "M", "L", "XL", "XXL", "XXXL"]),

            ("Tailles pantalons (28 à 46)", 20,
                ["28", "30", "32", "34", "36", "38", "40", "42", "44", "46"]),

            ("Pointures (35 à 47)", 30,
                ["35", "36", "37", "38", "39", "40", "41", "42", "43", "44", "45", "46", "47"]),

            ("Taille unique", 40, ["Unique"])
        ];

        var systemesExistants = await _contexte.SystemesTailles
            .ToDictionaryAsync(s => s.Nom, jeton)
            .ConfigureAwait(false);

        var ajoutes = 0;

        foreach (var (nom, ordre, _) in catalogue)
        {
            if (systemesExistants.ContainsKey(nom))
            {
                continue;
            }

            var systeme = new SystemeTaille { Nom = nom, Ordre = ordre, Actif = true };

            _contexte.SystemesTailles.Add(systeme);
            systemesExistants[nom] = systeme;
            ajoutes++;
        }

        if (ajoutes > 0)
        {
            await _contexte.SaveChangesAsync(jeton).ConfigureAwait(false);
            _journal.LogInformation("{Nombre} système(s) de tailles créé(s).", ajoutes);
        }

        var taillesExistantes = await _contexte.Tailles
            .Select(t => new { t.SystemeTailleId, t.Nom })
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        var connues = taillesExistantes
            .Select(t => (t.SystemeTailleId, t.Nom))
            .ToHashSet();

        var nouvelles = new List<Taille>();

        foreach (var (nom, _, tailles) in catalogue)
        {
            var systeme = systemesExistants[nom];

            for (var i = 0; i < tailles.Length; i++)
            {
                if (connues.Contains((systeme.Id, tailles[i])))
                {
                    continue;
                }

                nouvelles.Add(new Taille
                {
                    Nom = tailles[i],
                    SystemeTailleId = systeme.Id,
                    // Le pas de dix laisse la place d'intercaler une taille.
                    Ordre = (i + 1) * 10,
                    Actif = true
                });
            }
        }

        if (nouvelles.Count > 0)
        {
            _contexte.Tailles.AddRange(nouvelles);
            await _contexte.SaveChangesAsync(jeton).ConfigureAwait(false);
            _journal.LogInformation("{Nombre} taille(s) créée(s).", nouvelles.Count);
        }
    }

    /// <summary>
    /// Crée les familles d'articles d'un magasin de vêtements. Chacune
    /// désigne le système de tailles que la création d'une déclinaison
    /// proposera.
    /// </summary>
    private async Task AmorcerCategoriesAsync(CancellationToken jeton)
    {
        const string Vetements = "Tailles vêtements (XS à XXXL)";
        const string Pantalons = "Tailles pantalons (28 à 46)";
        const string Pointures = "Pointures (35 à 47)";
        const string Unique = "Taille unique";

        (string Nom, string Systeme, int Ordre)[] familles =
        [
            ("T-shirts et polos", Vetements, 10),
            ("Chemises", Vetements, 20),
            ("Pulls et sweats", Vetements, 30),
            ("Vestes et manteaux", Vetements, 40),
            ("Pantalons", Pantalons, 50),
            ("Jeans", Pantalons, 60),
            ("Shorts", Pantalons, 70),
            ("Costumes", Vetements, 80),
            ("Chaussures", Pointures, 90),
            ("Accessoires", Unique, 100)
        ];

        var systemes = await _contexte.SystemesTailles
            .ToDictionaryAsync(s => s.Nom, s => s.Id, jeton)
            .ConfigureAwait(false);

        var existantes = await _contexte.Categories
            .Select(c => c.Nom)
            .ToListAsync(jeton)
            .ConfigureAwait(false);

        var manquantes = familles
            .Where(f => !existantes.Contains(f.Nom) && systemes.ContainsKey(f.Systeme))
            .Select(f => new Categorie
            {
                Nom = f.Nom,
                SystemeTailleId = systemes[f.Systeme],
                Ordre = f.Ordre,
                Actif = true
            })
            .ToList();

        if (manquantes.Count > 0)
        {
            _contexte.Categories.AddRange(manquantes);
            await _contexte.SaveChangesAsync(jeton).ConfigureAwait(false);

            _journal.LogInformation("{Nombre} famille(s) d'articles créée(s).", manquantes.Count);
        }

        await MasquerFamilleDeRepriseAsync(jeton).ConfigureAwait(false);
    }

    /// <summary>
    /// Retire « Non classé » des listes lorsqu'aucun article ne s'y trouve.
    ///
    /// Cette famille n'existe que pour accueillir les articles d'une base
    /// antérieure aux familles. Sur une installation neuve elle ne sert à
    /// rien, et la proposer laisserait créer des articles sans rayon.
    /// </summary>
    private async Task MasquerFamilleDeRepriseAsync(CancellationToken jeton)
    {
        var reprise = await _contexte.Categories
            .FirstOrDefaultAsync(c => c.Nom == "Non classé", jeton)
            .ConfigureAwait(false);

        if (reprise is null)
        {
            return;
        }

        var utilisee = await _contexte.Produits
            .AnyAsync(p => p.CategorieId == reprise.Id, jeton)
            .ConfigureAwait(false);

        if (reprise.Actif == utilisee)
        {
            return;
        }

        reprise.Actif = utilisee;

        await _contexte.SaveChangesAsync(jeton).ConfigureAwait(false);

        _journal.LogInformation(
            utilisee
                ? "La famille « Non classé » reste proposée : des articles y sont rattachés."
                : "La famille « Non classé » est masquée : aucun article ne s'y trouve.");
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
            NomMagasin = "VIP MEN’S STORE",
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

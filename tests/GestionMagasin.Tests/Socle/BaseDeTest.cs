using GestionMagasin.Application.Common;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Entities;
using GestionMagasin.Domain.Interfaces;
using GestionMagasin.Domain.Securite;
using GestionMagasin.Infrastructure;
using GestionMagasin.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GestionMagasin.Tests.Socle;

/// <summary>
/// Socle commun des tests : crée une base PostgreSQL réelle et isolée pour
/// chaque classe de tests, applique les migrations puis amorce les données de
/// référence.
///
/// Les tests s'exécutent volontairement sur PostgreSQL et non sur une base en
/// mémoire : les règles qui garantissent la cohérence du stock (verrous
/// « FOR UPDATE », contraintes CHECK, index uniques, transactions) n'existent
/// que dans le vrai moteur. Les vérifier ailleurs ne prouverait rien.
/// </summary>
public abstract class BaseDeTest : IAsyncLifetime
{
    private const string ModeleChaineConnexion =
        "Host=127.0.0.1;Port=5432;Database={0};Username=vipwear;Password=vipwear;Include Error Detail=true";

    private readonly string _nomBase = $"test_magasin_{Guid.NewGuid():N}";

    private ServiceProvider _fournisseur = null!;

    protected string ChaineConnexion => string.Format(ModeleChaineConnexion, _nomBase);

    /// <summary>Portée de services représentant « l'écran » qui exécute le test.</summary>
    protected IServiceScope Portee { get; private set; } = null!;

    protected T Service<T>() where T : notnull => Portee.ServiceProvider.GetRequiredService<T>();

    protected SessionUtilisateur Session => _fournisseur.GetRequiredService<SessionUtilisateur>();

    /// <summary>
    /// Crée une portée de services indépendante, avec son propre contexte et
    /// sa propre connexion : l'équivalent d'un second poste de caisse.
    /// </summary>
    protected IServiceScope CreerPortee() => _fournisseur.CreateScope();

    public async Task InitializeAsync()
    {
        await CreerBaseAsync().ConfigureAwait(false);

        var services = new ServiceCollection();

        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AjouterGestionMagasin(ChaineConnexion);

        _fournisseur = services.BuildServiceProvider();

        using (var portee = _fournisseur.CreateScope())
        {
            var initialiseur = portee.ServiceProvider.GetRequiredService<InitialiseurBaseDonnees>();
            await initialiseur.InitialiserAsync().ConfigureAwait(false);
        }

        Portee = _fournisseur.CreateScope();

        await ConnecterAsync(NomsRoles.Administrateur).ConfigureAwait(false);
        await PreparerAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        Portee.Dispose();
        await _fournisseur.DisposeAsync().ConfigureAwait(false);

        // Les connexions doivent être fermées avant que la base puisse être
        // supprimée : le pool est vidé explicitement.
        Npgsql.NpgsqlConnection.ClearAllPools();

        await SupprimerBaseAsync().ConfigureAwait(false);
    }

    /// <summary>Point d'extension appelé une fois la base prête.</summary>
    protected virtual Task PreparerAsync() => Task.CompletedTask;

    /// <summary>
    /// Ouvre une session pour le rôle demandé, en créant le compte au besoin.
    /// Les services contrôlant les permissions, tous les tests doivent être
    /// authentifiés.
    /// </summary>
    protected async Task<Utilisateur> ConnecterAsync(string nomRole, string? nomUtilisateur = null)
    {
        var identifiant = nomUtilisateur ?? $"test_{nomRole.ToLowerInvariant()}";

        using var portee = _fournisseur.CreateScope();
        var contexte = portee.ServiceProvider.GetRequiredService<ContexteMagasin>();
        var hacheur = portee.ServiceProvider.GetRequiredService<IHacheurMotDePasse>();

        var utilisateur = await contexte.Utilisateurs
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.NomUtilisateur == identifiant)
            .ConfigureAwait(false);

        if (utilisateur is null)
        {
            var role = await contexte.Roles.FirstAsync(r => r.Nom == nomRole).ConfigureAwait(false);

            utilisateur = new Utilisateur
            {
                Nom = nomRole,
                Prenom = "Testeur",
                NomUtilisateur = identifiant,
                PasswordHash = hacheur.Hacher("MotDePasse2026"),
                RoleId = role.Id,
                Role = role,
                Actif = true,
                DateCreation = DateTime.UtcNow
            };

            contexte.Utilisateurs.Add(utilisateur);
            await contexte.SaveChangesAsync().ConfigureAwait(false);
        }

        var permissions = await contexte.RolePermissions
            .Where(rp => rp.RoleId == utilisateur.RoleId)
            .Select(rp => rp.Permission.Code)
            .ToListAsync()
            .ConfigureAwait(false);

        Session.Ouvrir(utilisateur, permissions);

        return utilisateur;
    }

    /// <summary>Relit une valeur directement en base, hors de tout cache mémoire.</summary>
    protected async Task<T> LireEnBaseAsync<T>(Func<ContexteMagasin, Task<T>> lecture)
    {
        using var portee = _fournisseur.CreateScope();
        var contexte = portee.ServiceProvider.GetRequiredService<ContexteMagasin>();

        return await lecture(contexte).ConfigureAwait(false);
    }

    /// <summary>Quantité actuellement en stock pour une déclinaison.</summary>
    protected Task<int> StockAsync(int varianteProduitId) =>
        LireEnBaseAsync(async contexte =>
            await contexte.Inventaires
                .Where(i => i.VarianteProduitId == varianteProduitId)
                .Select(i => (int?)i.QuantiteDisponible)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false) ?? 0);

    private async Task CreerBaseAsync()
    {
        await using var connexion = new Npgsql.NpgsqlConnection(
            string.Format(ModeleChaineConnexion, "postgres"));

        await connexion.OpenAsync().ConfigureAwait(false);

        await using var commande = connexion.CreateCommand();
        commande.CommandText = $"CREATE DATABASE \"{_nomBase}\"";
        await commande.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private async Task SupprimerBaseAsync()
    {
        await using var connexion = new Npgsql.NpgsqlConnection(
            string.Format(ModeleChaineConnexion, "postgres"));

        await connexion.OpenAsync().ConfigureAwait(false);

        await using var commande = connexion.CreateCommand();
        commande.CommandText = $"DROP DATABASE IF EXISTS \"{_nomBase}\" WITH (FORCE)";
        await commande.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}

using GestionMagasin.Infrastructure.Data;
using GestionMagasin.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Reprise d'une base déjà en exploitation.
///
/// La famille d'un article et le système d'une taille sont obligatoires. Or
/// la base d'un magasin qui tourne contient déjà des produits et des tailles
/// qui n'en ont pas. Une colonne ajoutée obligatoire d'emblée violerait sa
/// contrainte à la première ligne : la mise à jour échouerait, et le magasin
/// resterait bloqué au démarrage avec ses données inaccessibles.
///
/// Ce test rejoue exactement cette situation : ancienne base, données
/// dedans, puis mise à jour.
/// </summary>
public class TestsMigrationCategories : IAsyncLifetime
{
    private const string PremiereMigration = "20260816223323_CreationInitiale";

    private static readonly string ModeleChaineConnexion =
        Environment.GetEnvironmentVariable("GESTIONMAGASIN_TESTS_CONNEXION")
        ?? "Host=localhost;Port=5432;Database={0};Username=postgres;Password=postgres;Include Error Detail=true";

    private readonly string _nomBase = $"migration_{Guid.NewGuid():N}";

    private string ChaineConnexion => string.Format(ModeleChaineConnexion, _nomBase);

    public async Task InitializeAsync()
    {
        await using var connexion = new NpgsqlConnection(string.Format(ModeleChaineConnexion, "postgres"));
        await connexion.OpenAsync();

        await using var commande = connexion.CreateCommand();
        commande.CommandText = $"CREATE DATABASE \"{_nomBase}\"";
        await commande.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();

        await using var connexion = new NpgsqlConnection(string.Format(ModeleChaineConnexion, "postgres"));
        await connexion.OpenAsync();

        await using var commande = connexion.CreateCommand();
        commande.CommandText = $"DROP DATABASE IF EXISTS \"{_nomBase}\" WITH (FORCE)";
        await commande.ExecuteNonQueryAsync();
    }

    private ContexteMagasin CreerContexte() =>
        new(new DbContextOptionsBuilder<ContexteMagasin>()
            .UseNpgsql(ChaineConnexion, o => o.MigrationsAssembly(typeof(ContexteMagasin).Assembly.FullName))
            .Options);

    [Fact]
    public async Task Une_base_deja_remplie_se_met_a_jour_sans_rien_perdre()
    {
        // --- 1. La base telle qu'elle est chez le magasin -----------------
        await using (var ancienne = CreerContexte())
        {
            await ancienne.GetInfrastructure().GetRequiredService<IMigrator>()
                .MigrateAsync(PremiereMigration);

            await ancienne.Database.ExecuteSqlRawAsync("""
                INSERT INTO tailles (nom, ordre, actif)
                VALUES ('XS',10,true),('S',20,true),('M',30,true),('L',40,true);

                INSERT INTO couleurs (nom, code_couleur, actif)
                VALUES ('Bleu','#2C7BE5',true),('Gris','#808080',true);

                INSERT INTO produits (reference, sku, nom, prix_achat, prix_vente, actif, date_creation)
                VALUES ('2321','2321','pantalon',1220,1400,true, now());

                INSERT INTO variantes_produits (produit_id, taille_id, couleur_id, sku, seuil_minimum, actif)
                SELECT p.id, t.id, c.id, '2321-'||left(c.nom,3)||'-'||t.nom, 0, true
                  FROM produits p, tailles t, couleurs c
                 WHERE p.reference='2321';
                """);
        }

        // --- 2. La mise à jour livrée -------------------------------------
        await using (var miseAJour = CreerContexte())
        {
            await miseAJour.Database.MigrateAsync();
        }

        // --- 3. Rien n'a disparu, et tout est rattaché --------------------
        await using var verification = CreerContexte();

        Assert.Equal(1, await verification.Produits.CountAsync());
        Assert.Equal(4, await verification.Tailles.CountAsync());
        Assert.Equal(8, await verification.VariantesProduits.CountAsync());

        var produit = await verification.Produits
            .Include(p => p.Categorie).ThenInclude(c => c.SystemeTaille)
            .SingleAsync();

        Assert.Equal("Non classé", produit.Categorie.Nom);
        Assert.Equal("Tailles vêtements (XS à XXXL)", produit.Categorie.SystemeTaille.Nom);

        // Aucune taille orpheline : la contrainte serait sinon inapplicable.
        Assert.Empty(await verification.Tailles.Where(t => t.SystemeTailleId == 0).ToListAsync());

        var systemes = await verification.Tailles
            .Select(t => t.SystemeTaille.Nom)
            .Distinct()
            .ToListAsync();

        Assert.Equal(["Tailles vêtements (XS à XXXL)"], systemes);
    }

    /// <summary>
    /// Après la mise à jour, l'amorçage complète le catalogue : les pointures
    /// et les tailles de pantalon arrivent, sans toucher aux tailles déjà là.
    /// </summary>
    [Fact]
    public async Task L_amorçage_complete_le_catalogue_apres_la_mise_a_jour()
    {
        await using (var ancienne = CreerContexte())
        {
            await ancienne.GetInfrastructure().GetRequiredService<IMigrator>()
                .MigrateAsync(PremiereMigration);

            await ancienne.Database.ExecuteSqlRawAsync(
                "INSERT INTO tailles (nom, ordre, actif) VALUES ('M',30,true);");
        }

        await using var contexte = CreerContexte();
        await contexte.Database.MigrateAsync();

        var initialiseur = new InitialiseurBaseDonnees(
            contexte,
            new HacheurMotDePassePbkdf2(),
            new FournisseurHorodatage(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<InitialiseurBaseDonnees>.Instance);

        await initialiseur.AmorcerAsync();

        var pointures = await contexte.Tailles
            .Where(t => t.SystemeTaille.Nom == "Pointures (35 à 47)")
            .Select(t => t.Nom)
            .ToListAsync();

        Assert.Contains("42", pointures);

        var pantalons = await contexte.Tailles
            .Where(t => t.SystemeTaille.Nom == "Tailles pantalons (28 à 46)")
            .Select(t => t.Nom)
            .ToListAsync();

        Assert.Contains("38", pantalons);

        // La taille « M » existante n'a pas été dupliquée.
        Assert.Equal(1, await contexte.Tailles.CountAsync(t => t.Nom == "M"));

        // Les familles couvrent le rayon d'un magasin d'hommes.
        var familles = await contexte.Categories.Select(c => c.Nom).ToListAsync();

        Assert.Contains("Chemises", familles);
        Assert.Contains("Pantalons", familles);
        Assert.Contains("Chaussures", familles);
    }
}

// Verification : installation neuve, exactement le chemin de l'application.
using GestionMagasin.Infrastructure.Data;
using GestionMagasin.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GestionMagasin.Tests;

public class TestsInstallationNeuve : IAsyncLifetime
{
    private readonly string _nom = $"neuve_{Guid.NewGuid():N}";
    private string Chaine => $"Host=localhost;Port=5432;Database={_nom};Username=postgres;Password=postgres";

    public async Task InitializeAsync()
    {
        await using var c = new Npgsql.NpgsqlConnection("Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres");
        await c.OpenAsync();
        await using var k = c.CreateCommand();
        k.CommandText = $"CREATE DATABASE \"{_nom}\"";
        await k.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        Npgsql.NpgsqlConnection.ClearAllPools();
        await using var c = new Npgsql.NpgsqlConnection("Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres");
        await c.OpenAsync();
        await using var k = c.CreateCommand();
        k.CommandText = $"DROP DATABASE IF EXISTS \"{_nom}\" WITH (FORCE)";
        await k.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Une base vide doit se préparer entièrement : c'est le tout premier
    /// démarrage chez un nouveau client, celui qui n'a droit qu'à un essai.
    /// </summary>
    [Fact]
    public async Task Un_premier_demarrage_prepare_la_base_entierement()
    {
        await using var contexte = new ContexteMagasin(
            new DbContextOptionsBuilder<ContexteMagasin>()
                .UseNpgsql(Chaine, o => o.MigrationsAssembly(typeof(ContexteMagasin).Assembly.FullName))
                .Options);

        var initialiseur = new InitialiseurBaseDonnees(
            contexte,
            new HacheurMotDePassePbkdf2(),
            new FournisseurHorodatage(),
            NullLogger<InitialiseurBaseDonnees>.Instance);

        await initialiseur.InitialiserAsync();

        Assert.Empty(await contexte.Database.GetPendingMigrationsAsync());
        Assert.Equal(4, await contexte.SystemesTailles.CountAsync());
        Assert.Equal(7 + 10 + 13 + 1, await contexte.Tailles.CountAsync());

        // Les dix familles du rayon sont proposées. « Non classé », qui
        // n'existe que pour reprendre une base antérieure, est masquée :
        // aucun article ne s'y trouve sur une installation neuve.
        var proposees = await contexte.Categories.Where(c => c.Actif).Select(c => c.Nom).ToListAsync();

        Assert.Equal(10, proposees.Count);
        Assert.DoesNotContain("Non classé", proposees);
        Assert.Contains("Chaussures", proposees);
        Assert.True(await contexte.Utilisateurs.AnyAsync());
        Assert.True(await contexte.ParametresMagasin.AnyAsync());
    }
}

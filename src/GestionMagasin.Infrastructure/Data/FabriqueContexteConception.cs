using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GestionMagasin.Infrastructure.Data;

/// <summary>
/// Fabrique utilisée uniquement par les outils Entity Framework en ligne de
/// commande (« dotnet ef migrations add », « dotnet ef database update »).
/// L'application, elle, obtient son contexte par injection de dépendances.
/// La chaîne de connexion peut être surchargée par la variable
/// d'environnement GESTIONMAGASIN_CONNEXION.
/// </summary>
public class FabriqueContexteConception : IDesignTimeDbContextFactory<ContexteMagasin>
{
    private const string ConnexionParDefaut =
        "Host=localhost;Port=5432;Database=gestionmagasin;Username=postgres;Password=postgres";

    public ContexteMagasin CreateDbContext(string[] args)
    {
        var connexion = Environment.GetEnvironmentVariable("GESTIONMAGASIN_CONNEXION")
                        ?? ConnexionParDefaut;

        var options = new DbContextOptionsBuilder<ContexteMagasin>()
            .UseNpgsql(connexion, o => o.MigrationsAssembly(typeof(ContexteMagasin).Assembly.FullName))
            .Options;

        return new ContexteMagasin(options);
    }
}

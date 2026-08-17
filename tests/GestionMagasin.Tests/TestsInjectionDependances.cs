using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Infrastructure;
using GestionMagasin.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GestionMagasin.Tests;

/// <summary>
/// Vérifie la cohérence du conteneur d'injection de dépendances.
///
/// Ces contrôles n'ouvrent aucune connexion : ils portent sur la manière dont
/// les services sont enregistrés, pas sur ce qu'ils font. Ils protègent contre
/// deux erreurs coûteuses en production :
///
/// — la « dépendance captive », un service durable qui retient un service
///   censé être renouvelé, et qui garde donc le même contexte de données
///   pendant toute la durée de vie du logiciel ;
/// — l'oubli d'un enregistrement, qui ne se manifesterait qu'à l'ouverture de
///   l'écran concerné, chez le client.
/// </summary>
public class TestsInjectionDependances
{
    private const string ChaineFictive =
        "Host=localhost;Port=5432;Database=verification;Username=postgres;Password=postgres";

    private static ServiceProvider Construire()
    {
        var services = new ServiceCollection();

        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AjouterGestionMagasin(ChaineFictive);

        // Les mêmes contrôles que ceux activés en développement, appliqués ici
        // systématiquement : une portée ne peut pas être résolue depuis la
        // racine, et toutes les dépendances doivent être satisfaites.
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    [Fact]
    public void Le_conteneur_se_construit_sans_dependance_manquante()
    {
        using var fournisseur = Construire();

        Assert.NotNull(fournisseur);
    }

    [Theory]
    [InlineData(typeof(IServiceProduits))]
    [InlineData(typeof(IServiceStock))]
    [InlineData(typeof(IServiceVentes))]
    [InlineData(typeof(IServiceAchats))]
    [InlineData(typeof(IServiceRetours))]
    [InlineData(typeof(IServiceClients))]
    [InlineData(typeof(IServiceFournisseurs))]
    [InlineData(typeof(IServiceRapports))]
    [InlineData(typeof(IServiceUtilisateurs))]
    [InlineData(typeof(IServiceAuthentification))]
    [InlineData(typeof(IServiceParametres))]
    [InlineData(typeof(IServiceAudit))]
    [InlineData(typeof(IServiceDocumentsPdf))]
    [InlineData(typeof(IServiceExportExcel))]
    public void Chaque_service_metier_est_resolvable_dans_une_portee(Type service)
    {
        using var fournisseur = Construire();
        using var portee = fournisseur.CreateScope();

        Assert.NotNull(portee.ServiceProvider.GetRequiredService(service));
    }

    [Fact]
    public void Deux_ecrans_ne_partagent_jamais_le_meme_contexte_de_donnees()
    {
        using var fournisseur = Construire();

        using var premierEcran = fournisseur.CreateScope();
        using var secondEcran = fournisseur.CreateScope();

        var contexteA = premierEcran.ServiceProvider.GetRequiredService<ContexteMagasin>();
        var contexteB = secondEcran.ServiceProvider.GetRequiredService<ContexteMagasin>();

        // Le contexte d'Entity Framework n'accepte qu'une opération à la fois :
        // deux écrans qui le partageraient se gêneraient dès qu'ils liraient
        // en même temps.
        Assert.NotSame(contexteA, contexteB);
    }

    [Fact]
    public void Un_meme_ecran_reutilise_son_contexte_pour_tous_ses_services()
    {
        using var fournisseur = Construire();
        using var ecran = fournisseur.CreateScope();

        var contexte = ecran.ServiceProvider.GetRequiredService<ContexteMagasin>();
        var memeContexte = ecran.ServiceProvider.GetRequiredService<ContexteMagasin>();

        // Sans cela, une vente et son mouvement de stock ne pourraient pas
        // partager la même transaction.
        Assert.Same(contexte, memeContexte);
    }

    [Fact]
    public void Aucun_service_durable_ne_retient_un_contexte_de_donnees()
    {
        using var fournisseur = Construire();

        // ValidateScopes rejette la résolution d'un service « scoped » depuis
        // la racine : c'est exactement ce qui arriverait si un singleton en
        // dépendait.
        Assert.Throws<InvalidOperationException>(
            () => fournisseur.GetRequiredService<ContexteMagasin>());
    }
}

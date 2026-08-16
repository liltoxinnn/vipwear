using GestionMagasin.Application.Common;
using GestionMagasin.Application.Services;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Domain.Interfaces;
using GestionMagasin.Infrastructure.Data;
using GestionMagasin.Infrastructure.Documents;
using GestionMagasin.Infrastructure.Repositories;
using GestionMagasin.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;

namespace GestionMagasin.Infrastructure;

/// <summary>
/// Enregistrement de tous les services du logiciel auprès du conteneur
/// d'injection de dépendances. C'est le seul endroit où les implémentations
/// concrètes sont associées à leurs interfaces.
/// </summary>
public static class InjectionDependances
{
    /// <summary>
    /// Enregistre la base de données, les dépôts et l'ensemble des services
    /// métier. Les services suivent la durée de vie « scoped » : chaque écran
    /// ou chaque opération travaille avec son propre contexte, ce qui évite
    /// qu'une erreur sur un écran ne pollue les autres.
    /// </summary>
    public static IServiceCollection AjouterGestionMagasin(
        this IServiceCollection services,
        string chaineConnexion)
    {
        // QuestPDF est utilisé sous licence Community, gratuite pour cet usage.
        QuestPDF.Settings.License = LicenseType.Community;

        services.AddDbContext<ContexteMagasin>(options =>
        {
            options.UseNpgsql(chaineConnexion, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(ContexteMagasin).Assembly.FullName);

                // La reprise automatique sur erreur passagère
                // (EnableRetryOnFailure) est volontairement désactivée : elle
                // est incompatible avec les transactions ouvertes
                // explicitement par le logiciel, sur lesquelles repose toute
                // la cohérence du stock. Mieux vaut qu'une vente échoue
                // franchement, sans rien écrire, que d'être rejouée
                // partiellement.
                npgsql.CommandTimeout(60);
            });
        });

        // --- Briques techniques ---
        services.AddSingleton<IHacheurMotDePasse, HacheurMotDePassePbkdf2>();
        services.AddSingleton<IFournisseurHorodatage, FournisseurHorodatage>();

        // La session est unique pour tout le poste : un seul utilisateur est
        // connecté à la fois sur une caisse.
        services.AddSingleton<SessionUtilisateur>();
        services.AddSingleton<ISessionUtilisateur>(f => f.GetRequiredService<SessionUtilisateur>());

        // --- Accès aux données ---
        services.AddScoped<IUniteDeTravail, UniteDeTravail>();
        services.AddScoped<IGenerateurNumeros, GenerateurNumeros>();
        services.AddScoped<InitialiseurBaseDonnees>();

        // --- Services métier ---
        services.AddScoped<IServiceAudit, ServiceAudit>();
        services.AddScoped<IServiceStock, ServiceStock>();
        services.AddScoped<IServiceAuthentification, ServiceAuthentification>();
        services.AddScoped<IServiceUtilisateurs, ServiceUtilisateurs>();
        services.AddScoped<IServiceProduits, ServiceProduits>();
        services.AddScoped<IServiceVentes, ServiceVentes>();
        services.AddScoped<IServiceAchats, ServiceAchats>();
        services.AddScoped<IServiceRetours, ServiceRetours>();
        services.AddScoped<IServiceClients, ServiceClients>();
        services.AddScoped<IServiceFournisseurs, ServiceFournisseurs>();
        services.AddScoped<IServiceRapports, ServiceRapports>();

        services.AddScoped<IServiceParametres, ServiceParametres>();

        // La fiche magasin est mise en cache pour toute l'application : elle
        // est lue à chaque impression mais ne change presque jamais.
        services.AddSingleton<CacheParametresMagasin>();

        // --- Documents ---
        services.AddScoped<IServiceDocumentsPdf, ServiceDocumentsPdf>();
        services.AddScoped<IServiceExportExcel, ServiceExportExcel>();

        return services;
    }
}

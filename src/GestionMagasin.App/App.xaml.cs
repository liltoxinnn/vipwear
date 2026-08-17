using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using GestionMagasin.App.Converters;
using GestionMagasin.App.Services;
using GestionMagasin.App.ViewModels;
using GestionMagasin.App.ViewModels.Dialogues;
using GestionMagasin.App.Views;
using GestionMagasin.App.Views.Dialogues;
using GestionMagasin.Application.Common;
using GestionMagasin.Application.Services.Abstractions;
using GestionMagasin.Infrastructure;
using GestionMagasin.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace GestionMagasin.App;

/// <summary>
/// Point d'entrée de l'application. Met en place la configuration, la
/// journalisation, l'injection de dépendances, prépare la base de données
/// puis affiche la fenêtre de connexion.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _hote;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Toute l'interface est en français : dates, nombres et messages
        // système suivent cette culture.
        AppliquerCultureFrancaise();

        try
        {
            _hote = ConstruireHote();
            await _hote.StartAsync().ConfigureAwait(true);

            if (!await PreparerBaseDeDonneesAsync().ConfigureAwait(true))
            {
                Shutdown(1);
                return;
            }

            await ChargerParametresAffichageAsync().ConfigureAwait(true);

            // Les écrans ouvrent leurs fenêtres de saisie via le conteneur.
            Views.VueProduits.Fournisseur = _hote.Services;

            AfficherFenetreConnexion();
        }
        catch (Exception erreur)
        {
            Log.Fatal(erreur, "Le logiciel n'a pas pu démarrer.");

            MessageBox.Show(
                "Le logiciel n'a pas pu démarrer." + Environment.NewLine + Environment.NewLine +
                "Vérifiez que le serveur de base de données est accessible, puis relancez " +
                "l'application. Le détail de l'erreur a été enregistré dans le journal.",
                "Démarrage impossible",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_hote is not null)
        {
            await _hote.StopAsync().ConfigureAwait(false);
            _hote.Dispose();
        }

        await Log.CloseAndFlushAsync().ConfigureAwait(false);

        base.OnExit(e);
    }

    // ------------------------------------------------------------------

    private static IHost ConstruireHote()
    {
        var dossierApplication = AppContext.BaseDirectory;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(dossierApplication)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables("GESTIONMAGASIN_")
            .Build();

        var dossierJournaux = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GestionMagasin",
            "journaux");

        Directory.CreateDirectory(dossierJournaux);

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(dossierJournaux, "gestionmagasin-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        var chaineConnexion = configuration.GetConnectionString("BaseDonnees")
            ?? throw new InvalidOperationException(
                "La chaîne de connexion « BaseDonnees » est absente du fichier appsettings.json.");

        return Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(services =>
            {
                services.AjouterGestionMagasin(chaineConnexion);

                // --- Services propres à l'interface ---
                services.AddSingleton<IServiceDialogue, ServiceDialogue>();
                services.AddSingleton<IServiceNavigation, ServiceNavigation>();

                // --- Fenêtres ---
                services.AddTransient<FenetreConnexion>();
                services.AddSingleton<FenetrePrincipale>();
                services.AddTransient<FenetreChangementMotDePasse>();
                services.AddTransient<FenetreProduit>();
                services.AddTransient<FenetreVariante>();
                services.AddTransient<FenetreGenerationVariantes>();
                services.AddTransient<FenetreMouvementStock>();
                services.AddTransient<FenetreAchat>();
                services.AddTransient<FenetreReception>();

                // --- Vues-modèles ---
                services.AddTransient<VueModeleConnexion>();
                services.AddSingleton<VueModelePrincipale>();
                services.AddTransient<VueModeleTableauBord>();
                services.AddTransient<VueModeleCaisse>();
                services.AddTransient<VueModeleProduits>();
                services.AddTransient<VueModeleStock>();
                services.AddTransient<VueModeleVentes>();
                services.AddTransient<VueModeleAchats>();
                services.AddTransient<VueModeleFournisseurs>();
                services.AddTransient<VueModeleClients>();
                services.AddTransient<VueModeleRetours>();
                services.AddTransient<VueModeleRapports>();
                services.AddTransient<VueModeleUtilisateurs>();
                services.AddTransient<VueModeleParametres>();

                // --- Vues-modèles des fenêtres de saisie ---
                services.AddTransient<VueModeleFormulaireProduit>();
                services.AddTransient<VueModeleFormulaireVariante>();
                services.AddTransient<VueModeleGenerationVariantes>();
                services.AddTransient<VueModeleMouvementStock>();
                services.AddTransient<VueModeleFormulaireAchat>();
                services.AddTransient<VueModeleReception>();
            })
            .Build();
    }

    /// <summary>
    /// Applique les migrations et amorce les données de référence. Retourne
    /// faux si la base est inaccessible, auquel cas le logiciel s'arrête avec
    /// un message explicite.
    /// </summary>
    private async Task<bool> PreparerBaseDeDonneesAsync()
    {
        using var portee = _hote!.Services.CreateScope();

        var journal = portee.ServiceProvider.GetRequiredService<ILogger<App>>();

        try
        {
            var initialiseur = portee.ServiceProvider.GetRequiredService<InitialiseurBaseDonnees>();
            await initialiseur.InitialiserAsync().ConfigureAwait(true);

            return true;
        }
        catch (Exception erreur)
        {
            journal.LogCritical(erreur, "Préparation de la base de données impossible.");

            MessageBox.Show(
                "La connexion à la base de données a échoué." + Environment.NewLine + Environment.NewLine +
                "Vérifiez que le serveur PostgreSQL est démarré et que les informations de " +
                "connexion du fichier « appsettings.json » sont correctes.",
                "Base de données inaccessible",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return false;
        }
    }

    /// <summary>Reprend le symbole de devise du magasin pour l'affichage des montants.</summary>
    private async Task ChargerParametresAffichageAsync()
    {
        using var portee = _hote!.Services.CreateScope();

        var parametres = portee.ServiceProvider.GetRequiredService<IServiceParametres>();
        var magasin = await parametres.ObtenirAsync().ConfigureAwait(true);

        ConvertisseurMontant.SymboleDevise = magasin.SymboleDevise;
    }

    private void AfficherFenetreConnexion()
    {
        var connexion = _hote!.Services.GetRequiredService<FenetreConnexion>();

        connexion.Show();
    }

    /// <summary>
    /// Force la culture française sur les fils d'exécution et sur le moteur
    /// de rendu WPF, afin que dates et nombres s'affichent au format attendu.
    /// </summary>
    private static void AppliquerCultureFrancaise()
    {
        var culture = FormatageMontant.CultureApplication;

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));
    }

    /// <summary>
    /// Dernier filet de sécurité : une erreur non interceptée est journalisée
    /// et signalée sans faire disparaître l'application en pleine vente.
    /// </summary>
    private void SurExceptionNonGeree(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Erreur non interceptée dans l'interface.");

        MessageBox.Show(
            "Une erreur inattendue est survenue." + Environment.NewLine + Environment.NewLine +
            "L'opération en cours a été interrompue. Vos données enregistrées ne sont pas affectées.",
            "Erreur inattendue",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    public App()
    {
        DispatcherUnhandledException += SurExceptionNonGeree;
    }
}

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
using GestionMagasin.Infrastructure.Services;
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

        try
        {
            // La connexion est vérifiée avant tout le reste : à la première
            // installation, le magasin est invité à la configurer plutôt que
            // de subir un message d'erreur technique.
            if (!await GarantirConnexionAsync().ConfigureAwait(true))
            {
                Shutdown(0);
                return;
            }

            _hote = ConstruireHote();
            await _hote.StartAsync().ConfigureAwait(true);

            if (!await PreparerBaseDeDonneesAsync().ConfigureAwait(true))
            {
                Shutdown(1);
                return;
            }

            await ChargerParametresAffichageAsync().ConfigureAwait(true);

            // Les écrans ouvrent leurs fenêtres de saisie via cette fabrique,
            // qui donne à chacune sa propre portée de services.
            Views.VueProduits.Fabrique = _hote.Services.GetRequiredService<IFabriqueFenetres>();

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

    /// <summary>
    /// S'assure qu'une base de données joignable est configurée. Si ce n'est
    /// pas le cas, la fenêtre de configuration est proposée à l'utilisateur.
    /// </summary>
    /// <returns>Faux si l'utilisateur renonce et quitte le logiciel.</returns>
    private static async Task<bool> GarantirConnexionAsync()
    {
        var chaine = LireChaineConnexion();

        if (!string.IsNullOrWhiteSpace(chaine))
        {
            var resultat = await TesteurConnexion.VerifierEtPreparerAsync(chaine).ConfigureAwait(true);

            if (resultat.Reussie)
            {
                return true;
            }

            Log.Warning("Base de données injoignable : {Message}", resultat.Message);
        }

        var fenetre = new FenetreConfigurationBaseDonnees(chaine);

        return fenetre.ShowDialog() == true;
    }

    /// <summary>Relit la chaîne de connexion effective des fichiers de configuration.</summary>
    private static string? LireChaineConnexion() =>
        new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables("GESTIONMAGASIN_")
            .Build()
            .GetConnectionString("BaseDonnees");

    /// <summary>
    /// Dossier où sont écrits les journaux techniques. Il est communiqué à
    /// l'utilisateur en cas d'erreur, pour qu'il sache quoi transmettre.
    /// </summary>
    internal static string DossierJournaux { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GestionMagasin",
        "journaux");

    private static IHost ConstruireHote()
    {
        var dossierApplication = AppContext.BaseDirectory;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(dossierApplication)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Local.json", optional: true)
            .AddEnvironmentVariables("GESTIONMAGASIN_")
            .Build();

        var dossierJournaux = DossierJournaux;

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
                services.AddSingleton<IFabriqueFenetres, FabriqueFenetres>();

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
                "La préparation de la base de données a échoué." + Environment.NewLine + Environment.NewLine +
                "Vérifiez que le serveur PostgreSQL est démarré et que le compte utilisé a le " +
                "droit de créer des tables. Relancez ensuite le logiciel : la fenêtre de " +
                "configuration vous sera proposée.",
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

    private static bool _cultureAppliquee;

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

        // OverrideMetadata n'accepte qu'un seul appel par propriété : le
        // second lèverait une exception au démarrage.
        if (_cultureAppliquee)
        {
            return;
        }

        _cultureAppliquee = true;

        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));
    }

    // Une erreur survenue pendant l'affichage se reproduit à chaque passe de
    // rendu. Comme la boîte de dialogue continue de faire tourner la boucle de
    // messages de Windows, la même erreur reviendrait pendant qu'elle est
    // ouverte et empilerait des dizaines de fenêtres devant l'utilisateur.
    // Ces deux garde-fous garantissent qu'un incident n'est signalé qu'une fois.
    private bool _signalementEnCours;
    private string? _derniereErreur;
    private DateTime _derniereErreurLe;

    /// <summary>
    /// Dernier filet de sécurité : une erreur non interceptée est journalisée
    /// et signalée sans faire disparaître l'application en pleine vente.
    /// </summary>
    private void SurExceptionNonGeree(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // L'application reste en vie : une caisse ne doit pas se fermer au
        // milieu d'un encaissement.
        e.Handled = true;

        Log.Error(e.Exception, "Erreur non interceptée dans l'interface.");

        if (_signalementEnCours)
        {
            return;
        }

        var signature = e.Exception.GetType().FullName + "|" + e.Exception.StackTrace;
        var maintenant = DateTime.UtcNow;

        if (signature == _derniereErreur
            && maintenant - _derniereErreurLe < TimeSpan.FromSeconds(15))
        {
            return;
        }

        _derniereErreur = signature;
        _derniereErreurLe = maintenant;
        _signalementEnCours = true;

        try
        {
            MessageBox.Show(
                ComposerMessageErreur(e.Exception),
                "Erreur inattendue",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _signalementEnCours = false;
        }
    }

    /// <summary>
    /// Rédige le message affiché : la cause réelle y figure, ainsi que l'endroit
    /// où trouver le détail technique. Sans cela, l'utilisateur n'a rien à
    /// transmettre pour faire corriger le problème.
    /// </summary>
    private static string ComposerMessageErreur(Exception erreur)
    {
        // Une règle métier non respectée est déjà rédigée pour l'utilisateur.
        var cause = erreur is Domain.Exceptions.ExceptionMetier
            ? erreur.Message
            : $"{erreur.Message} ({erreur.GetType().Name})";

        return
            "Une erreur inattendue est survenue." + Environment.NewLine + Environment.NewLine +
            "Détail : " + cause + Environment.NewLine + Environment.NewLine +
            "L'opération en cours a été interrompue. Vos données enregistrées ne sont pas affectées." +
            Environment.NewLine + Environment.NewLine +
            "Le détail technique a été enregistré dans :" + Environment.NewLine +
            DossierJournaux;
    }

    public App()
    {
        // La culture est fixée avant toute création de fenêtre : les libellés
        // et les formats de date en dépendent dès le premier affichage.
        AppliquerCultureFrancaise();

        DispatcherUnhandledException += SurExceptionNonGeree;
    }
}

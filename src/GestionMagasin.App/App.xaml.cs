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
using GestionMagasin.ServeurEmbarque;
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

    /// <summary>
    /// Serveur de base de données démarré par le logiciel lui-même, lorsque
    /// les fichiers de PostgreSQL sont livrés à côté de l'exécutable. Le
    /// magasin n'installe alors rien et ne saisit aucun mot de passe.
    /// </summary>
    private ServeurPostgresEmbarque? _serveur;

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

            // Le serveur embarqué, s'il y en a un, est démarré avant la
            // construction de l'hôte : la chaîne de connexion en dépend.
            if (!await DemarrerServeurEmbarqueAsync().ConfigureAwait(true))
            {
                Shutdown(1);
                return;
            }

            _hote = ConstruireHote(_serveur);
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

        // Le serveur est arrêté après l'hôte : les connexions du logiciel
        // doivent être refermées avant lui.
        if (_serveur is not null)
        {
            try
            {
                await _serveur.ArreterAsync().ConfigureAwait(false);
            }
            catch (Exception erreur)
            {
                Log.Error(erreur, "Arrêt du serveur embarqué impossible.");
            }
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
        // Rien à demander : le logiciel héberge sa propre base de données.
        if (LocalisateurOutils.ServeurEmbarquePresent())
        {
            return true;
        }

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

    /// <summary>
    /// Démarre le serveur livré avec le logiciel, s'il y en a un. La chaîne de
    /// connexion obtenue est publiée dans l'environnement du processus, d'où
    /// la configuration la relira : le reste du logiciel ignore ainsi
    /// complètement d'où vient sa base de données.
    /// </summary>
    /// <returns>Faux si le serveur n'a pas pu démarrer.</returns>
    private async Task<bool> DemarrerServeurEmbarqueAsync()
    {
        if (!LocalisateurOutils.ServeurEmbarquePresent())
        {
            return true;
        }

        try
        {
            _serveur = new ServeurPostgresEmbarque(
                journaliser: message => Log.Information("Serveur embarqué : {Message}", message));

            var chaine = await _serveur.DemarrerAsync().ConfigureAwait(true);

            Environment.SetEnvironmentVariable("GESTIONMAGASIN_ConnectionStrings__BaseDonnees", chaine);

            return true;
        }
        catch (Exception erreur)
        {
            Log.Fatal(erreur, "Le serveur de base de données livré n'a pas pu démarrer.");

            // La sortie des programmes PostgreSQL est en anglais : elle part
            // dans le journal, à l'intention du prestataire, et n'est jamais
            // montrée au magasin.
            if (erreur is ServeurEmbarqueException { DetailTechnique: { } detail })
            {
                Log.Fatal("Sortie de PostgreSQL :{NouvelleLigne}{Detail}", Environment.NewLine, detail);
            }

            MessageBox.Show(
                erreur.Message + Environment.NewLine + Environment.NewLine +
                "Si le problème persiste, transmettez le journal à votre prestataire :" +
                Environment.NewLine + DossierJournaux,
                "Base de données indisponible",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return false;
        }
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

    private static IHost ConstruireHote(ServeurPostgresEmbarque? serveur)
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
                // Écriture immédiate : si le logiciel s'arrête brutalement, les
                // dernières lignes sont précisément celles qui expliquent
                // pourquoi. Mises en attente, elles seraient perdues.
                flushToDiskInterval: TimeSpan.FromMilliseconds(500),
                outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        var chaineConnexion = configuration.GetConnectionString("BaseDonnees");

        // La configuration livrée au magasin part sans identifiants : une
        // valeur vide compte comme absente, sans quoi elle serait transmise
        // telle quelle au pilote PostgreSQL.
        if (string.IsNullOrWhiteSpace(chaineConnexion))
        {
            throw new InvalidOperationException(
                "La chaîne de connexion « BaseDonnees » n'est pas renseignée.");
        }

        return Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(services =>
            {
                services.AjouterGestionMagasin(chaineConnexion);

                // La sauvegarde n'est proposée que lorsque le logiciel héberge
                // sa propre base : c'est alors le seul moyen pour le magasin de
                // protéger ses données, pgAdmin n'étant pas installé.
                if (serveur is not null)
                {
                    services.AddSingleton(serveur);
                    services.AddSingleton<ServiceSauvegarde>();
                }

                // --- Services propres à l'interface ---
                services.AddSingleton<IServiceDialogue, ServiceDialogue>();
                services.AddSingleton<IServiceNavigation, ServiceNavigation>();
                services.AddSingleton<IFabriqueFenetres, FabriqueFenetres>();

                // --- Fenêtres ---
                // Ces deux fenêtres sont recréées à chaque session : une
                // fenêtre WPF fermée ne peut plus être réaffichée. Les garder
                // uniques empêcherait toute reconnexion après déconnexion.
                services.AddTransient<FenetreConnexion>();
                services.AddTransient<FenetrePrincipale>();
                services.AddTransient<FenetreChangementMotDePasse>();
                services.AddTransient<FenetreProduit>();
                services.AddTransient<FenetreVariante>();
                services.AddTransient<FenetreGenerationVariantes>();
                services.AddTransient<FenetreMouvementStock>();
                services.AddTransient<FenetreAchat>();
                services.AddTransient<FenetreReception>();

                // --- Vues-modèles ---
                services.AddTransient<VueModeleConnexion>();
                services.AddTransient<VueModelePrincipale>();
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

    /// <summary>
    /// Dernier recours pour une erreur survenue hors du fil de l'interface.
    ///
    /// Windows termine alors le processus : le logiciel se ferme d'un coup,
    /// en pleine vente, sans rien afficher. On ne peut pas l'empêcher, mais on
    /// peut écrire ce qui s'est passé et le dire à l'utilisateur, sans quoi
    /// l'incident reste incompréhensible et introuvable.
    /// </summary>
    private static void SurErreurFatale(object sender, UnhandledExceptionEventArgs e)
    {
        var erreur = e.ExceptionObject as Exception;

        Log.Fatal(erreur, "Arrêt brutal du logiciel (fil d'exécution secondaire).");
        Log.CloseAndFlush();

        try
        {
            MessageBox.Show(
                "Le logiciel a dû s'arrêter." + Environment.NewLine + Environment.NewLine +
                "Détail : " + (erreur?.Message ?? "cause inconnue") + Environment.NewLine + Environment.NewLine +
                "Les opérations déjà enregistrées sont conservées : une vente validée " +
                "reste validée, son stock est déjà décompté." + Environment.NewLine + Environment.NewLine +
                "Le détail technique a été enregistré dans :" + Environment.NewLine + DossierJournaux,
                "Arrêt du logiciel",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch (Exception)
        {
            // Le processus se termine de toute façon : rien de plus à tenter.
        }
    }

    /// <summary>
    /// Erreur dans une tâche dont personne n'a lu le résultat. Sans ce
    /// traitement, elle reste invisible jusqu'au passage du ramasse-miettes,
    /// où elle peut alors terminer le processus.
    /// </summary>
    private static void SurTacheNonObservee(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Erreur dans une tâche dont le résultat n'a pas été lu.");

        e.SetObserved();
    }

    public App()
    {
        // La culture est fixée avant toute création de fenêtre : les libellés
        // et les formats de date en dépendent dès le premier affichage.
        AppliquerCultureFrancaise();

        DispatcherUnhandledException += SurExceptionNonGeree;

        // Le gestionnaire ci-dessus ne couvre que le fil de l'interface. Une
        // erreur survenue ailleurs — accès aux données, génération d'un
        // document, pilote de base — fermerait le logiciel sans laisser de
        // trace.
        AppDomain.CurrentDomain.UnhandledException += SurErreurFatale;
        TaskScheduler.UnobservedTaskException += SurTacheNonObservee;
    }
}
